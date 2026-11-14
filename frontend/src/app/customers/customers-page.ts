import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, ElementRef, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';
import { EMPTY, Subject, catchError, debounceTime, finalize, merge, switchMap, tap } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AuthService } from '../core/auth.service';
import { Customer, CustomerApi, CustomerContact, CustomerLocation } from './customer-api';

const isValidCpfCnpj = (digits: string): boolean => {
  if (/^(\d)\1+$/.test(digits)) return false;
  if (digits.length === 11) {
    const check = (length: number): number => {
      const sum = digits.slice(0, length).split('').reduce((total, digit, index) => total + Number(digit) * (length + 1 - index), 0);
      const result = (sum * 10) % 11;
      return result === 10 ? 0 : result;
    };
    return Number(digits[9]) === check(9) && Number(digits[10]) === check(10);
  }
  if (digits.length === 14) {
    const check = (base: string, weights: number[]): number => {
      const sum = base.split('').reduce((total, digit, index) => total + Number(digit) * weights[index], 0);
      const remainder = sum % 11;
      return remainder < 2 ? 0 : 11 - remainder;
    };
    const first = check(digits.slice(0, 12), [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
    const second = check(digits.slice(0, 12) + first, [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
    return Number(digits[12]) === first && Number(digits[13]) === second;
  }
  return false;
};

const cpfCnpjValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const digits = String(control.value ?? '').replace(/\D/g, '');
  return !digits || isValidCpfCnpj(digits) ? null : { cpfCnpj: true };
};

const isGoogleMapsUrl = (value: string): boolean => {
  try {
    const url = new URL(value.trim());
    if (url.protocol !== 'https:') return false;
    const host = url.hostname.toLowerCase();
    if (host === 'maps.app.goo.gl' || host === 'maps.google.com') return true;
    if (host === 'goo.gl') return url.pathname.toLowerCase().startsWith('/maps/');
    return (host === 'google.com' || host === 'www.google.com') && url.pathname.toLowerCase().startsWith('/maps/');
  } catch {
    return false;
  }
};

const googleMapsUrlValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value = String(control.value ?? '').trim();
  return !value || isGoogleMapsUrl(value) ? null : { googleMapsUrl: true };
};

@Component({
  selector: 'app-customers-page',
  imports: [ReactiveFormsModule],
  templateUrl: './customers-page.html',
  styleUrl: './customers-page.css',
})
export class CustomersPage implements OnInit {
  private readonly customerDialog = viewChild<ElementRef<HTMLDialogElement>>('customerDialog');
  private returnFocusTarget: HTMLElement | null = null;
  private readonly destroyRef = inject(DestroyRef);
  private readonly customerSearchRequests = new Subject<void>();
  private readonly api = inject(CustomerApi);
  private readonly formBuilder = inject(FormBuilder);
  readonly auth = inject(AuthService);
  readonly customers = signal<Customer[]>([]);
  readonly selected = signal<Customer | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly page = signal(1);
  readonly pageSize = signal(12);
  readonly totalCount = signal(0);
  readonly rangeStart = computed(() => this.totalCount() === 0 ? 0 : (this.page() - 1) * this.pageSize() + 1);
  readonly rangeEnd = computed(() => Math.min(this.page() * this.pageSize(), this.totalCount()));
  readonly viewMode = signal<'cards' | 'table'>('cards');
  readonly sortBy = signal<'name' | 'city'>('name');
  readonly descending = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly locations = signal<CustomerLocation[]>([]);
  readonly responsibleContactOptions = signal<CustomerContact[]>([]);
  readonly locationsLoading = signal(false);
  readonly locationSaving = signal(false);
  readonly locationError = signal('');
  readonly locationNotice = signal('');
  readonly editingLocationId = signal<string | null>(null);
  readonly postalCodeLoading = signal(false);
  readonly postalCodeError = signal('');
  readonly contactLocationId = signal<string | null>(null);
  readonly contactFilterScope = signal('all');
  readonly contacts = signal<CustomerContact[]>([]);
  readonly contactCounts = signal<Record<string, number> | null>(null);
  readonly contactsLoading = signal(false);
  readonly contactSaving = signal(false);
  readonly contactError = signal('');
  readonly contactNotice = signal('');
  readonly editingContactId = signal<string | null>(null);
  readonly deletingContactId = signal<string | null>(null);
  readonly detailsTab = signal<'overview' | 'locations' | 'contacts'>('overview');
  readonly query = this.formBuilder.nonNullable.control('');
  readonly cityFilter = this.formBuilder.nonNullable.control('');
  readonly brazilianStates = [
    { code: 'AC', name: 'Acre' }, { code: 'AL', name: 'Alagoas' }, { code: 'AP', name: 'Amapá' },
    { code: 'AM', name: 'Amazonas' }, { code: 'BA', name: 'Bahia' }, { code: 'CE', name: 'Ceará' },
    { code: 'DF', name: 'Distrito Federal' }, { code: 'ES', name: 'Espírito Santo' }, { code: 'GO', name: 'Goiás' },
    { code: 'MA', name: 'Maranhão' }, { code: 'MT', name: 'Mato Grosso' }, { code: 'MS', name: 'Mato Grosso do Sul' },
    { code: 'MG', name: 'Minas Gerais' }, { code: 'PA', name: 'Pará' }, { code: 'PB', name: 'Paraíba' },
    { code: 'PR', name: 'Paraná' }, { code: 'PE', name: 'Pernambuco' }, { code: 'PI', name: 'Piauí' },
    { code: 'RJ', name: 'Rio de Janeiro' }, { code: 'RN', name: 'Rio Grande do Norte' }, { code: 'RS', name: 'Rio Grande do Sul' },
    { code: 'RO', name: 'Rondônia' }, { code: 'RR', name: 'Roraima' }, { code: 'SC', name: 'Santa Catarina' },
    { code: 'SP', name: 'São Paulo' }, { code: 'SE', name: 'Sergipe' }, { code: 'TO', name: 'Tocantins' },
  ];
  readonly editing = signal(false);
  readonly canManage = () => this.auth.session()?.roles.some(role => role === 'Administrador' || role === 'Gestor') ?? false;
  readonly form = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    document: ['', [Validators.maxLength(40), Validators.pattern(/^[0-9./\-\s]*$/), cpfCnpjValidator]],
    instructions: ['', Validators.maxLength(2000)],
    postalCode: ['', Validators.maxLength(9)],
    address: ['', Validators.maxLength(500)],
    locality: ['', Validators.maxLength(200)],
    city: ['', Validators.maxLength(200)],
    state: ['', [Validators.maxLength(2), (control) => control.value && !this.isValidBrazilianState(control.value) ? { stateCode: true } : null]],
  });
  readonly locationForm = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    address: ['', [Validators.required, Validators.maxLength(500)]],
    neighborhood: ['', Validators.maxLength(200)],
    city: ['', Validators.maxLength(200)],
    mapUrl: ['', [Validators.maxLength(2000), googleMapsUrlValidator]],
    status: ['Active' as CustomerLocation['status'], Validators.required],
    instructions: ['', Validators.maxLength(2000)],
    responsibleContactId: [''],
  });
  readonly contactForm = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    role: ['', Validators.maxLength(200)],
    phone: ['', Validators.maxLength(40)],
    email: ['', [Validators.email, Validators.maxLength(254)]],
    isPrimary: [false],
  });
  private contactLoadSequence = 0;

  ngOnInit(): void {
    this.form.controls.postalCode.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(value => {
      const formatted = this.formatPostalCode(value);
      if (formatted !== value) this.form.controls.postalCode.setValue(formatted, { emitEvent: false });
    });
    this.form.controls.document.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(value => {
      const formatted = this.maskDocumentInput(value);
      if (formatted !== value) this.form.controls.document.setValue(formatted, { emitEvent: false });
    });
    this.contactForm.controls.phone.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(value => {
      const formatted = this.formatBrazilianPhone(value);
      if (formatted !== value) this.contactForm.controls.phone.setValue(formatted, { emitEvent: false });
    });
    this.customerSearchRequests.pipe(
      switchMap(() => {
        this.error.set('');
        this.loading.set(true);
        return this.api.search(this.query.value, this.page(), this.pageSize(), this.cityFilter.value, this.sortBy(), this.descending()).pipe(
          tap(result => { this.customers.set(result.items); this.totalCount.set(result.totalCount); }),
          catchError(() => { this.error.set('Não foi possível carregar os clientes. Tente novamente.'); return EMPTY; }),
          finalize(() => this.loading.set(false)),
        );
      }),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe();

    merge(this.query.valueChanges, this.cityFilter.valueChanges).pipe(
      debounceTime(300), takeUntilDestroyed(this.destroyRef),
    ).subscribe(() => {
      this.page.set(1);
      this.selected.set(null);
      this.load();
    });
    this.load();
  }

  search(): void { this.page.set(1); this.selected.set(null); this.load(); }

  clearFilters(): void {
    this.query.setValue('');
    this.cityFilter.setValue('');
    this.page.set(1);
    this.load();
  }

  load(): void { this.customerSearchRequests.next(); }

  setPageSize(value: string): void {
    this.pageSize.set(Number(value));
    this.page.set(1);
    this.selected.set(null);
    this.load();
  }

  setSort(field: 'name' | 'city'): void {
    if (this.sortBy() === field) this.descending.update(value => !value);
    else { this.sortBy.set(field); this.descending.set(false); }
    this.page.set(1);
    this.load();
  }

  formatDocument(document: string | null): string {
    if (!document?.trim()) return '';
    const digits = document.replace(/\D/g, '');
    if (!isValidCpfCnpj(digits)) return document.trim();
    if (digits.length === 11) return `CPF ${digits.slice(0, 3)}.${digits.slice(3, 6)}.${digits.slice(6, 9)}-${digits.slice(9)}`;
    if (digits.length === 14) return `CNPJ ${digits.slice(0, 2)}.${digits.slice(2, 5)}.${digits.slice(5, 8)}/${digits.slice(8, 12)}-${digits.slice(12)}`;
    return document.trim();
  }

  maskDocumentInput(value: string): string {
    if (!/^[0-9./\-\s]*$/.test(value)) return value;
    const digits = value.replace(/\D/g, '').slice(0, 14);
    if (digits.length <= 11) return digits.replace(/^(\d{3})(\d)/, '$1.$2').replace(/^(\d{3}\.\d{3})(\d)/, '$1.$2').replace(/^(\d{3}\.\d{3}\.\d{3})(\d{1,2})$/, '$1-$2');
    return digits.replace(/^(\d{2})(\d)/, '$1.$2').replace(/^(\d{2}\.\d{3})(\d)/, '$1.$2').replace(/^(\d{2}\.\d{3}\.\d{3})(\d)/, '$1/$2').replace(/^(\d{2}\.\d{3}\.\d{3}\/\d{4})(\d{1,2})$/, '$1-$2');
  }

  hasDocument(document: string | null): boolean { return Boolean(document?.trim()); }

  isValidDocument(document: string | null): boolean {
    if (!document?.trim() || !/^[0-9./\-\s]*$/.test(document)) return false;
    return isValidCpfCnpj(document.replace(/\D/g, ''));
  }

  documentValidationMessage(): string {
    const value = this.form.controls.document.value;
    if (!/^[0-9./\-\s]*$/.test(value)) return 'Documento inválido. Use apenas números e pontuação de CPF/CNPJ.';
    return value.replace(/\D/g, '').length > 11
      ? 'CNPJ inválido. Confira os 14 dígitos e os dígitos verificadores.'
      : 'CPF inválido. Confira os 11 dígitos e os dígitos verificadores.';
  }

  formatPostalCode(value: string): string {
    const digits = value.replace(/\D/g, '').slice(0, 8);
    return digits.length > 5 ? `${digits.slice(0, 5)}-${digits.slice(5)}` : digits;
  }

  canLookupPostalCode(): boolean { return this.form.controls.postalCode.value.replace(/\D/g, '').length === 8 && !this.postalCodeLoading(); }

  isValidBrazilianState(value: string): boolean { return this.brazilianStates.some(state => state.code === value); }

  avatarBackground(name: string): string { return `hsl(${this.avatarHue(name)} 48% 90%)`; }
  avatarForeground(name: string): string { return `hsl(${this.avatarHue(name)} 46% 27%)`; }
  private avatarHue(name: string): number {
    let hash = 0;
    for (const character of name) hash = (hash * 31 + character.charCodeAt(0)) >>> 0;
    return hash % 360;
  }

  whatsappUrl(phone: string | null | undefined): string | null {
    if (!phone) return null;
    const digits = phone.replace(/\D/g, '');
    const hasBrazilCountryPrefix = digits.startsWith('55') && (digits.length === 12 || digits.length === 13);
    if (!hasBrazilCountryPrefix && digits.length !== 10 && digits.length !== 11) return null;
    return `https://wa.me/${digits.startsWith('55') ? digits : `55${digits}`}`;
  }

  formatBrazilianPhone(value: string): string {
    let digits = value.replace(/\D/g, '');
    if (digits.startsWith('55') && digits.length > 11) digits = digits.slice(2);
    digits = digits.slice(0, 11);
    if (digits.length <= 2) return digits ? `(${digits}` : '';
    if (digits.length <= 6) return `(${digits.slice(0, 2)}) ${digits.slice(2)}`;
    if (digits.length <= 10) return `(${digits.slice(0, 2)}) ${digits.slice(2, 6)}-${digits.slice(6)}`;
    return `(${digits.slice(0, 2)}) ${digits.slice(2, 7)}-${digits.slice(7)}`;
  }

  contactEmptyMessage(): string {
    const scope = this.contactFilterScope();
    if (scope === 'all') return 'Nenhum contato cadastrado ainda. Adicione o engenheiro ou o responsável da obra.';
    if (scope === 'client') return 'Nenhum contato vinculado diretamente ao cliente.';
    return 'Nenhum contato neste local.';
  }

  contactScopeLabel(): string {
    const scope = this.contactFilterScope();
    if (scope === 'client') return 'Cliente';
    if (scope === 'all') return 'Cliente';
    return this.locations().find(location => location.id === scope)?.name ?? 'Local de atendimento';
  }

  mapsUrl(location: CustomerLocation): string {
    if (location.mapUrl) return location.mapUrl;
    const query = [location.name, location.address, location.neighborhood, location.city].filter(Boolean).join(', ');
    return `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(query)}`;
  }

  locationAddressSummary(location: CustomerLocation): string {
    const normalize = (value: string): string => value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().replace(/[^a-z0-9]/g, '');
    const nameDuplicatesAddress = normalize(location.name) === normalize(location.address);
    const details = [location.neighborhood, location.city].filter(Boolean);
    return (nameDuplicatesAddress ? details : [location.address, ...details]).filter(Boolean).join(', ');
  }

  customerAddressSummary(customer: Customer): string {
    const cityState = [customer.city, customer.state].filter(Boolean).join('/');
    return [customer.address, customer.locality, cityState].filter(Boolean).join(' · ') || 'Endereço não informado';
  }

  customerMapsUrl(customer: Customer): string | null {
    const query = [customer.address, customer.locality, customer.city, customer.state, customer.postalCode].filter(Boolean).join(', ');
    return query ? `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(query)}` : null;
  }

  formatCustomerDate(value: string): string {
    return new Intl.DateTimeFormat('pt-BR', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
  }

  availableResponsibleContacts(): CustomerContact[] {
    const locationId = this.editingLocationId();
    return this.responsibleContactOptions().filter(contact => !contact.locationId || contact.locationId === locationId);
  }

  select(customer: Customer): void {
    this.detailsTab.set('overview');
    this.selected.set(customer);
    this.locations.set([]);
    this.editing.set(false);
    this.error.set('');
    this.notice.set('');
    this.contactLocationId.set(null);
    this.contactFilterScope.set('all');
    this.contacts.set([]);
    this.contactCounts.set(null);
    this.contactError.set('');
    this.loadContacts();
    this.loadLocations(customer.id);
    this.openCustomerDialog();
  }

  handleDialogCancel(event: Event): void {
    if (this.hasUnsavedChanges() && !window.confirm('Descartar as alterações ainda não salvas?')) event.preventDefault();
  }

  handleDialogBackdropClick(event: MouseEvent): void {
    if (event.target === event.currentTarget) this.closeCustomerDialog();
  }

  closeCustomerDialog(): void {
    if (this.hasUnsavedChanges() && !window.confirm('Descartar as alterações ainda não salvas?')) return;
    const dialog = this.customerDialog()?.nativeElement;
    if (!dialog) return;
    if (typeof dialog.close === 'function') dialog.close();
    else {
      dialog.removeAttribute('open');
      this.onCustomerDialogClose();
    }
  }

  onCustomerDialogClose(): void {
    this.selected.set(null);
    this.editing.set(false);
    this.editingLocationId.set(null);
    this.editingContactId.set(null);
    this.detailsTab.set('overview');
    const target = this.returnFocusTarget;
    this.returnFocusTarget = null;
    queueMicrotask(() => { if (target?.isConnected) target.focus(); });
  }

  private openCustomerDialog(): void {
    const dialog = this.customerDialog()?.nativeElement;
    if (dialog && !dialog.open) {
      const activeElement = document.activeElement;
      this.returnFocusTarget = activeElement instanceof HTMLElement ? activeElement : null;
      if (typeof dialog.showModal === 'function') dialog.showModal();
      else dialog.setAttribute('open', '');
    }
  }

  private hasUnsavedChanges(): boolean {
    return (this.editing() && this.form.dirty)
      || (this.editingLocationId() !== null && this.locationForm.dirty)
      || (this.editingContactId() !== null && this.contactForm.dirty);
  }

  showCustomerSection(customer: Customer, tab: 'locations' | 'contacts'): void {
    this.select(customer);
    this.detailsTab.set(tab);
  }

  editCustomer(customer: Customer): void {
    this.select(customer);
    this.startEdit();
  }

  startCreate(): void {
    this.selected.set(null);
    this.detailsTab.set('overview');
    this.form.reset({ name: '', document: '', instructions: '', postalCode: '', address: '', locality: '', city: '', state: '' });
    this.editing.set(true);
    this.error.set('');
    this.notice.set('');
    this.openCustomerDialog();
  }

  startEdit(): void {
    const customer = this.selected();
    if (!customer || !this.canManage()) return;
    this.form.reset({ name: customer.name, document: customer.document ?? '', instructions: customer.instructions ?? '', postalCode: customer.postalCode ?? '',
      address: customer.address ?? '', locality: customer.locality ?? '', city: customer.city ?? '', state: this.isValidBrazilianState(customer.state ?? '') ? customer.state! : '' });
    this.editing.set(true);
    this.error.set('');
    this.notice.set('');
  }

  cancelEdit(): void {
    if (this.form.dirty && !window.confirm('Descartar as alterações ainda não salvas?')) return;
    this.editing.set(false);
    if (!this.selected()) this.customerDialog()?.nativeElement.close();
  }

  save(): void {
    this.error.set('');
    if (this.form.invalid || this.saving() || !this.canManage()) {
      this.form.markAllAsTouched();
      return;
    }
    const selected = this.selected();
    const values = this.form.getRawValue();
    const input = { name: values.name.trim(), document: values.document.trim() || null,
      instructions: values.instructions.trim() || null, postalCode: values.postalCode.trim() || null, address: values.address.trim() || null,
      locality: values.locality.trim() || null, city: values.city.trim() || null, state: values.state.trim() || null };
    this.saving.set(true);
    const request = selected ? this.api.update(selected.id, input) : this.api.create(input);
    request.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: customer => {
        this.selected.set(customer);
        this.editing.set(false);
        this.notice.set(selected ? 'Cliente atualizado.' : 'Cliente cadastrado.');
        this.loadLocations(customer.id);
        this.load();
      },
      error: (error: HttpErrorResponse) => {
        const validationErrors = error.error?.errors as Record<string, string[]> | undefined;
        if (error.status === 400 && validationErrors?.['Document']) {
          this.error.set('CPF/CNPJ inválido. Informe um documento numérico válido, com ou sem pontuação.');
        } else {
          this.error.set(error.status === 400
            ? 'Confira o nome, documento, instruções e endereço principal do cliente.'
            : 'Não foi possível salvar o cliente. Verifique a conexão e tente novamente.');
        }
      },
    });
  }

  previousPage(): void {
    if (this.page() <= 1 || this.loading()) return;
    this.selected.set(null);
    this.locations.set([]);
    this.page.update(value => value - 1);
    this.load();
  }

  nextPage(): void {
    if (this.page() * this.pageSize() >= this.totalCount() || this.loading()) return;
    this.selected.set(null);
    this.locations.set([]);
    this.page.update(value => value + 1);
    this.load();
  }

  trackCustomer(_: number, customer: Customer): string { return customer.id; }

  lookupPostalCode(): void {
    const digits = this.form.controls.postalCode.value.replace(/\D/g, '');
    this.postalCodeError.set('');
    if (digits.length !== 8) { this.postalCodeError.set('Informe um CEP com 8 dígitos.'); return; }
    this.postalCodeLoading.set(true);
    this.api.lookupPostalCode(digits).pipe(finalize(() => this.postalCodeLoading.set(false))).subscribe({
      next: result => {
        if (result.erro) { this.postalCodeError.set('CEP não encontrado. Confira o número e tente novamente.'); return; }
        this.form.patchValue({ postalCode: digits, address: result.logradouro || this.form.controls.address.value,
          locality: result.bairro || this.form.controls.locality.value, city: result.localidade || this.form.controls.city.value,
          state: result.uf || this.form.controls.state.value });
      },
      error: () => this.postalCodeError.set('Não foi possível consultar o CEP. Preencha o endereço manualmente.'),
    });
  }

  loadLocations(customerId: string): void {
    this.locations.set([]);
    this.locationError.set('');
    this.locationsLoading.set(true);
    this.api.locations(customerId).pipe(finalize(() => this.locationsLoading.set(false))).subscribe({
      next: locations => { this.locations.set(locations); if (this.contactCounts() !== null) this.loadContacts(); },
      error: () => this.locationError.set('Não foi possível carregar os locais deste cliente.'),
    });
  }

  changeContactScope(scope: string): void {
    this.contactFilterScope.set(scope);
    this.contactLocationId.set(scope === 'all' || scope === 'client' ? null : scope);
    this.editingContactId.set(null);
    this.loadContacts();
  }

  loadContacts(): void {
    const customer = this.selected();
    if (!customer) return;
    const requestSequence = ++this.contactLoadSequence;
    this.contactError.set('');
    this.contacts.set([]);
    this.contactCounts.set(null);
    this.contactsLoading.set(true);
    this.api.contacts(customer.id, null, true).pipe(finalize(() => {
      if (requestSequence === this.contactLoadSequence) this.contactsLoading.set(false);
    })).subscribe({
      next: allContacts => {
        if (requestSequence !== this.contactLoadSequence) return;
        const counts: Record<string, number> = { all: allContacts.length, client: allContacts.filter(contact => !contact.locationId).length };
        for (const location of this.locations()) counts[location.id] = allContacts.filter(contact => contact.locationId === location.id).length;
        this.contactCounts.set(counts);
        const scope = this.contactFilterScope();
        this.contacts.set(scope === 'all' ? allContacts : scope === 'client'
          ? allContacts.filter(contact => !contact.locationId)
          : allContacts.filter(contact => contact.locationId === scope));
      },
      error: () => { if (requestSequence === this.contactLoadSequence) this.contactError.set('Não foi possível carregar os contatos.'); },
    });
  }

  startCreateContact(): void {
    if (!this.canManage()) return;
    if (this.contactFilterScope() === 'all') this.contactFilterScope.set('client');
    this.contactLocationId.set(this.contactFilterScope() === 'client' ? null : this.contactFilterScope());
    this.contactForm.reset({ name: '', role: '', phone: '', email: '', isPrimary: false });
    this.editingContactId.set('new');
    this.contactError.set('');
    this.loadContacts();
  }

  startEditContact(contact: CustomerContact): void {
    if (!this.canManage()) return;
    this.contactFilterScope.set(contact.locationId ?? 'client');
    this.contactLocationId.set(contact.locationId);
    this.contactForm.reset({ name: contact.name, role: contact.role ?? '', phone: contact.phone ?? '', email: contact.email ?? '', isPrimary: contact.isPrimary });
    this.editingContactId.set(contact.id);
    this.contactError.set('');
    this.loadContacts();
  }

  cancelContactEdit(): void { this.editingContactId.set(null); }

  saveContact(): void {
    const customer = this.selected();
    if (!customer || !this.canManage() || this.contactForm.invalid || this.contactSaving()) {
      this.contactForm.markAllAsTouched();
      return;
    }
    const id = this.editingContactId();
    const values = this.contactForm.getRawValue();
    const input = { locationId: this.contactLocationId(), name: values.name.trim(), role: values.role.trim() || null,
      phone: values.phone.trim() || null, email: values.email.trim() || null, isPrimary: values.isPrimary };
    this.contactSaving.set(true);
    const request = id && id !== 'new' ? this.api.updateContact(customer.id, id, input) : this.api.createContact(customer.id, input);
    request.pipe(finalize(() => this.contactSaving.set(false))).subscribe({
      next: () => { this.editingContactId.set(null); this.contactNotice.set(id === 'new' ? 'Contato cadastrado.' : 'Contato atualizado.'); this.loadContacts(); },
      error: () => this.contactError.set('Não foi possível salvar o contato. Confira os campos e tente novamente.'),
    });
  }

  deleteContact(contact: CustomerContact): void {
    const customer = this.selected();
    if (!customer || !this.canManage() || this.deletingContactId()) return;
    const locationName = contact.locationId
      ? this.locations().find(location => location.id === contact.locationId)?.name ?? 'local de atendimento'
      : 'cadastro do cliente';
    const confirmed = window.confirm(`Excluir o contato "${contact.name}" do ${locationName} de "${customer.name}"? Esta ação não pode ser desfeita.`);
    if (!confirmed) return;
    this.deletingContactId.set(contact.id);
    this.contactError.set('');
    this.api.deleteContact(customer.id, contact.id).pipe(finalize(() => this.deletingContactId.set(null))).subscribe({
      next: () => {
        this.contactNotice.set(`Contato ${contact.name} excluído.`);
        this.loadContacts();
      },
      error: (error: HttpErrorResponse) => this.contactError.set(error.status === 409
        ? error.error?.message ?? 'Retire este contato como responsável pelo local antes de excluí-lo.'
        : 'Não foi possível excluir o contato. Atualize a lista e tente novamente.'),
    });
  }

  startCreateLocation(): void {
    if (!this.canManage() || !this.selected()) return;
    this.editingLocationId.set('new');
    this.locationForm.reset({ name: '', address: '', neighborhood: '', city: '', mapUrl: '', status: 'Active', instructions: '', responsibleContactId: '' });
    this.locationError.set('');
    this.locationNotice.set('');
    this.loadResponsibleContactOptions();
  }

  startEditLocation(location: CustomerLocation): void {
    if (!this.canManage()) return;
    this.editingLocationId.set(location.id);
    this.locationForm.reset({ name: location.name, address: location.address, neighborhood: location.neighborhood ?? '',
      city: location.city ?? '', mapUrl: location.mapUrl ?? '', status: location.status, instructions: location.instructions ?? '', responsibleContactId: location.responsibleContactId ?? '' });
    this.locationError.set('');
    this.locationNotice.set('');
    this.loadResponsibleContactOptions();
  }

  private loadResponsibleContactOptions(): void {
    const customer = this.selected();
    if (!customer) return;
    this.api.contacts(customer.id, null, true).subscribe({
      next: contacts => this.responsibleContactOptions.set(contacts),
      error: () => this.locationError.set('Não foi possível carregar os contatos responsáveis disponíveis.'),
    });
  }

  cancelLocationEdit(): void { this.editingLocationId.set(null); }

  saveLocation(): void {
    const customer = this.selected();
    this.locationError.set('');
    if (!customer || !this.canManage() || this.locationForm.invalid || this.locationSaving()) {
      this.locationForm.markAllAsTouched();
      return;
    }
    const values = this.locationForm.getRawValue();
    const input = { name: values.name.trim(), address: values.address.trim(), neighborhood: values.neighborhood.trim() || null,
      city: values.city.trim() || null, status: values.status, instructions: values.instructions.trim() || null, mapUrl: values.mapUrl.trim() || null,
      responsibleContactId: values.responsibleContactId || null };
    const id = this.editingLocationId();
    this.locationSaving.set(true);
    const request = id && id !== 'new' ? this.api.updateLocation(customer.id, id, input) : this.api.createLocation(customer.id, input);
    request.pipe(finalize(() => this.locationSaving.set(false))).subscribe({
      next: () => {
        this.editingLocationId.set(null);
        this.locationNotice.set(id === 'new' ? 'Local cadastrado.' : 'Local atualizado.');
        this.loadLocations(customer.id);
      },
      error: (error: HttpErrorResponse) => this.locationError.set(error.status === 400
        ? 'Informe o nome e o endereço do local e confira os limites dos campos.'
        : 'Não foi possível salvar o local. Verifique a conexão e tente novamente.'),
    });
  }
}
