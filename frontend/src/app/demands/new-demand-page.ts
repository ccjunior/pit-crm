import { HttpErrorResponse } from '@angular/common/http';
import { Component, EventEmitter, Input, OnInit, Output, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { Customer, CustomerApi, CustomerLocation } from '../customers/customer-api';
import { CreatedDemand, DemandApi, DemandDetails, DemandInput, EligibleAssignee, ExternalReferenceInput, UpdatedDemand } from './demand-api';

@Component({
  selector: 'app-new-demand-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './new-demand-page.html',
  styleUrl: './new-demand-page.css',
})
export class NewDemandPage implements OnInit {
  @Output() readonly cancelled = new EventEmitter<void>();
  @Output() readonly saved = new EventEmitter<CreatedDemand>();
  @Output() readonly updated = new EventEmitter<UpdatedDemand>();
  @Input() embedded = false;
  @Input() existingDemand: DemandDetails | null = null;
  private readonly formBuilder = inject(FormBuilder);
  private readonly customersApi = inject(CustomerApi);
  private readonly demandApi = inject(DemandApi);
  readonly form = this.formBuilder.nonNullable.group({
    customerId: ['', Validators.required],
    locationId: this.formBuilder.nonNullable.control({ value: '', disabled: true }, Validators.required),
    description: ['', [Validators.required, Validators.maxLength(4000), control =>
      typeof control.value === 'string' && control.value.trim() ? null : { required: true }]],
    origin: ['', Validators.required],
    assignedToUserId: [''],
    priority: [''],
  });
  readonly externalReferences = signal<ExternalReferenceInput[]>([]);
  readonly externalReferenceTypes = ['Chamado/OS', 'Ordem de Compra', 'Código do Cliente', 'Contrato', 'Outro'];
  readonly customerSearch = this.formBuilder.nonNullable.control('');
  readonly customers = signal<Customer[]>([]);
  readonly totalCustomerMatches = signal(0);
  readonly locations = signal<CustomerLocation[]>([]);
  readonly assignees = signal<EligibleAssignee[]>([]);
  readonly searchingCustomers = signal(false);
  readonly loadingLocations = signal(false);
  readonly loadingAssignees = signal(false);
  readonly submitting = signal(false);
  readonly reloadingDemand = signal(false);
  readonly submitted = signal(false);
  readonly versionConflict = signal(false);
  readonly customerPickerOpen = signal(false);
  readonly activeCustomerIndex = signal(0);
  readonly pageError = signal('');
  readonly submitError = signal('');
  readonly fieldErrors = signal<Record<string, string[]>>({});
  readonly externalReferenceErrors = signal<Record<number, string>>({});
  readonly created = signal<CreatedDemand | null>(null);
  private customerSearchRequestId = 0;
  private locationRequestId = 0;

  ngOnInit(): void {
    if (this.existingDemand) this.applyDemandToForm(this.existingDemand);
    this.searchCustomers();
    this.loadAssignees();
  }

  searchCustomers(): void {
    const requestId = ++this.customerSearchRequestId;
    this.pageError.set('');
    this.searchingCustomers.set(true);
    this.customersApi.search(this.customerSearch.value, 1, 100)
      .pipe(finalize(() => {
        if (requestId === this.customerSearchRequestId) this.searchingCustomers.set(false);
      }))
      .subscribe({
        next: result => {
          if (requestId !== this.customerSearchRequestId) return;
          this.customers.set(result.items);
          this.totalCustomerMatches.set(result.totalCount);
          const selectedCustomerId = this.form.controls.customerId.value;
          const retainedEditCustomer = this.existingDemand?.customerId === selectedCustomerId;
          if (!retainedEditCustomer && !result.items.some(customer => customer.id === selectedCustomerId)) {
            this.form.controls.customerId.setValue('');
            this.form.controls.locationId.setValue('');
            this.locations.set([]);
          }
        },
        error: () => {
          if (requestId === this.customerSearchRequestId) {
            this.pageError.set('Não foi possível carregar clientes. Tente novamente.');
          }
        },
      });
  }

  onCustomerSearchInput(): void {
    if (this.form.controls.customerId.value) {
      this.form.controls.customerId.setValue('');
      this.onCustomerChange();
    }
    this.fieldErrors.set({});
    this.submitError.set('');
    this.customers.set([]);
    this.totalCustomerMatches.set(0);
    this.customerPickerOpen.set(true);
    this.activeCustomerIndex.set(0);
    this.searchCustomers();
  }

  openCustomerPicker(): void {
    this.customerPickerOpen.set(true);
    const selectedIndex = this.customers().findIndex(customer => customer.id === this.form.controls.customerId.value);
    this.activeCustomerIndex.set(Math.max(selectedIndex, 0));
  }

  closeCustomerPicker(): void {
    this.form.controls.customerId.markAsTouched();
    this.customerPickerOpen.set(false);
  }

  onCustomerKeydown(event: KeyboardEvent): void {
    const options = this.customerSearch.value.trim() ? this.customers() : [];
    if (event.key === 'ArrowDown' && this.customerPickerOpen()) {
      event.preventDefault();
      if (options.length) this.activeCustomerIndex.update(index => Math.min(index + 1, options.length - 1));
    } else if (event.key === 'ArrowUp' && this.customerPickerOpen()) {
      event.preventDefault();
      if (options.length) this.activeCustomerIndex.update(index => Math.max(index - 1, 0));
    } else if (event.key === 'Escape' && this.customerPickerOpen()) {
      event.preventDefault();
      this.customerPickerOpen.set(false);
    } else if (event.key === 'Enter' && this.customerPickerOpen() && options.length) {
      event.preventDefault();
      this.selectCustomer(options[Math.min(this.activeCustomerIndex(), options.length - 1)]);
    }
  }

  selectCustomer(customer: Customer): void {
    this.form.controls.customerId.setValue(customer.id);
    this.form.controls.customerId.markAsTouched();
    this.customerSearch.setValue(this.customerDisplayName(customer));
    this.customerPickerOpen.set(false);
    this.fieldErrors.set({});
    this.onCustomerChange();
  }

  customerDisplayName(customer: Customer): string {
    return customer.document ? `${customer.name} · ${customer.document}` : customer.name;
  }

  loadAssignees(): void {
    this.loadingAssignees.set(true);
    this.demandApi.eligibleAssignees().pipe(finalize(() => this.loadingAssignees.set(false))).subscribe({
      next: users => this.assignees.set(users),
      error: () => this.pageError.set('Não foi possível carregar os responsáveis elegíveis.'),
    });
  }

  currentAssigneeUnavailable(): boolean {
    const assignedToUserId = this.existingDemand?.assignedToUserId;
    return !!assignedToUserId && !this.assignees().some(assignee => assignee.id === assignedToUserId);
  }

  onCustomerChange(): void {
    this.loadLocations(this.form.controls.customerId.value, null);
  }

  private loadLocations(customerId: string, selectedLocationId: string | null): void {
    const requestId = ++this.locationRequestId;
    this.form.controls.locationId.reset({ value: '', disabled: true });
    this.locations.set([]);
    this.submitError.set('');
    if (!customerId) {
      this.loadingLocations.set(false);
      return;
    }
    this.loadingLocations.set(true);
    this.customersApi.locations(customerId).pipe(finalize(() => {
      if (requestId === this.locationRequestId) this.loadingLocations.set(false);
    })).subscribe({
      next: locations => {
        if (requestId !== this.locationRequestId) return;
        this.locations.set(locations);
        if (locations.length) {
          this.form.controls.locationId.enable();
          if (selectedLocationId && locations.some(location => location.id === selectedLocationId)) {
            this.form.controls.locationId.setValue(selectedLocationId);
          } else if (locations.length === 1) {
            this.form.controls.locationId.setValue(locations[0].id);
          }
        }
      },
      error: () => {
        if (requestId === this.locationRequestId) this.pageError.set('Não foi possível carregar os locais deste cliente.');
      },
    });
  }

  private applyDemandToForm(demand: DemandDetails): void {
    this.form.patchValue({
      customerId: demand.customerId,
      locationId: '',
      description: demand.description,
      origin: demand.origin,
      assignedToUserId: demand.assignedToUserId ?? '',
      priority: demand.priority ?? '',
    });
    this.externalReferences.set(demand.externalReferences.map(reference => ({ ...reference })));
    this.externalReferenceErrors.set({});
    this.customerSearch.setValue(demand.customerName);
    this.loadLocations(demand.customerId, demand.locationId);
  }

  reloadCurrentDemand(): void {
    const current = this.existingDemand;
    if (!current || this.reloadingDemand()) return;
    this.reloadingDemand.set(true);
    this.demandApi.find(current.id).pipe(finalize(() => this.reloadingDemand.set(false))).subscribe({
      next: demand => {
        this.existingDemand = demand;
        this.versionConflict.set(false);
        this.submitError.set('');
        this.fieldErrors.set({});
        this.submitted.set(false);
        this.applyDemandToForm(demand);
      },
      error: () => this.pageError.set('Não foi possível recarregar a demanda. Seus dados continuam preenchidos.'),
    });
  }

  locationPlaceholder(): string {
    if (this.loadingLocations()) return 'Carregando locais…';
    if (!this.form.controls.customerId.value) return 'Selecione um cliente primeiro';
    if (!this.locations().length) return 'Nenhum local disponível';
    return 'Selecione o local';
  }

  showFieldError(name: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[name];
    if (name === 'locationId' && !control.value) return this.submitted() || control.touched;
    return control.invalid && (control.touched || this.submitted());
  }

  isRequirementComplete(name: 'customerId' | 'locationId' | 'description' | 'origin'): boolean {
    const value = this.form.controls[name].value;
    return typeof value === 'string' && value.trim().length > 0;
  }

  submitFromShortcut(event: Event): void {
    if (!(event instanceof KeyboardEvent) || !event.ctrlKey) return;
    event.preventDefault();
    this.submit();
  }

  submit(): void {
    this.submitted.set(true);
    this.submitError.set('');
    this.fieldErrors.set({});
    const referencesValid = this.validateExternalReferences();
    if (this.submitting() || this.form.invalid || !this.form.controls.locationId.value || !referencesValid) {
      this.form.markAllAsTouched();
      this.customerPickerOpen.set(false);
      return;
    }
    const values = this.form.getRawValue();
    const input: DemandInput = {
      customerId: values.customerId,
      locationId: values.locationId,
      description: values.description.trim(),
      origin: values.origin,
      assignedToUserId: values.assignedToUserId || null,
      externalReferences: this.externalReferences().filter(reference => reference.type.trim() && reference.value.trim())
        .map(reference => ({ type: reference.type.trim(), value: reference.value.trim() })),
      priority: values.priority || null,
    };
    this.submitting.set(true);
    this.versionConflict.set(false);
    if (this.existingDemand) {
      this.demandApi.update(this.existingDemand.id, { ...input, version: this.existingDemand.version })
        .pipe(finalize(() => this.submitting.set(false)))
        .subscribe({
          next: demand => this.updated.emit(demand),
          error: error => this.handleSaveError(error),
        });
      return;
    }

    this.demandApi.create(input).pipe(finalize(() => this.submitting.set(false))).subscribe({
      next: demand => {
        this.created.set(demand);
        if (this.embedded) this.saved.emit(demand);
      },
      error: error => this.handleSaveError(error),
    });
  }

  addExternalReference(): void {
    this.externalReferences.update(references => [...references, { type: '', value: '' }]);
  }

  updateExternalReference(index: number, field: keyof ExternalReferenceInput, value: string): void {
    this.externalReferences.update(references => references.map((reference, currentIndex) =>
      currentIndex === index ? { ...reference, [field]: value } : reference));
    this.externalReferenceErrors.update(errors => {
      const next = { ...errors };
      delete next[index];
      return next;
    });
  }

  removeExternalReference(index: number): void {
    this.externalReferences.update(references => references.filter((_, currentIndex) => currentIndex !== index));
    this.externalReferenceErrors.update(errors => Object.fromEntries(Object.entries(errors)
      .filter(([key]) => Number(key) !== index)
      .map(([key, value]) => [Number(key) > index ? Number(key) - 1 : Number(key), value])));
  }

  private validateExternalReferences(): boolean {
    const errors: Record<number, string> = {};
    this.externalReferences().forEach((reference, index) => {
      const hasType = !!reference.type.trim();
      const hasValue = !!reference.value.trim();
      if (!hasType && !hasValue) return;
      if (!hasType || !hasValue) errors[index] = 'Preencha o tipo e o valor da referência.';
      else if (reference.type.trim().length > 50) errors[index] = 'O tipo deve ter até 50 caracteres.';
      else if (reference.value.trim().length > 100) errors[index] = 'O valor deve ter até 100 caracteres.';
    });
    this.externalReferenceErrors.set(errors);
    return Object.keys(errors).length === 0;
  }

  private handleSaveError(error: HttpErrorResponse): void {
    const apiErrors = (error.error as { errors?: Record<string, string[]> } | null)?.errors;
    if (apiErrors) this.fieldErrors.set(apiErrors);
    if (error.status === 409 && this.existingDemand) {
      this.versionConflict.set(true);
      this.submitError.set(typeof error.error?.error === 'string'
        ? error.error.error
        : 'Esta demanda foi alterada por outra pessoa. Recarregue os dados atuais antes de salvar novamente.');
      return;
    }
    this.submitError.set(error.status === 400
      ? 'Revise os campos destacados e confirme que o local pertence ao cliente selecionado. Seus dados continuam preenchidos.'
      : 'Não foi possível confirmar a gravação. Seus dados foram mantidos; confira a demanda antes de tentar novamente.');
  }

  fieldError(name: string): string | null {
    const match = Object.entries(this.fieldErrors()).find(([key]) => key.toLowerCase() === name.toLowerCase());
    return match?.[1][0] ?? null;
  }
}
