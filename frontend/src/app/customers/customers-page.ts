import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { AuthService } from '../core/auth.service';
import { Customer, CustomerApi, CustomerLocation } from './customer-api';

@Component({
  selector: 'app-customers-page',
  imports: [ReactiveFormsModule],
  templateUrl: './customers-page.html',
  styleUrl: './customers-page.css',
})
export class CustomersPage implements OnInit {
  private readonly api = inject(CustomerApi);
  private readonly formBuilder = inject(FormBuilder);
  readonly auth = inject(AuthService);
  readonly customers = signal<Customer[]>([]);
  readonly selected = signal<Customer | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly page = signal(1);
  readonly totalCount = signal(0);
  readonly error = signal('');
  readonly notice = signal('');
  readonly locations = signal<CustomerLocation[]>([]);
  readonly locationsLoading = signal(false);
  readonly locationSaving = signal(false);
  readonly locationError = signal('');
  readonly locationNotice = signal('');
  readonly editingLocationId = signal<string | null>(null);
  readonly query = this.formBuilder.nonNullable.control('');
  readonly editing = signal(false);
  readonly canManage = () => this.auth.session()?.roles.some(role => role === 'Administrador' || role === 'Gestor') ?? false;
  readonly form = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    document: ['', Validators.maxLength(30)],
    instructions: ['', Validators.maxLength(2000)],
  });
  readonly locationForm = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    address: ['', [Validators.required, Validators.maxLength(500)]],
    instructions: ['', Validators.maxLength(2000)],
  });

  ngOnInit(): void { this.load(); }

  search(): void { this.page.set(1); this.selected.set(null); this.load(); }

  load(): void {
    this.error.set('');
    this.loading.set(true);
    this.api.search(this.query.value, this.page()).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: result => { this.customers.set(result.items); this.totalCount.set(result.totalCount); },
      error: () => this.error.set('Não foi possível carregar os clientes. Tente novamente.'),
    });
  }

  select(customer: Customer): void {
    this.selected.set(customer);
    this.locations.set([]);
    this.editing.set(false);
    this.error.set('');
    this.notice.set('');
    this.loadLocations(customer.id);
  }

  startCreate(): void {
    this.selected.set(null);
    this.form.reset({ name: '', document: '', instructions: '' });
    this.editing.set(true);
    this.error.set('');
    this.notice.set('');
  }

  startEdit(): void {
    const customer = this.selected();
    if (!customer || !this.canManage()) return;
    this.form.reset({ name: customer.name, document: customer.document ?? '', instructions: customer.instructions ?? '' });
    this.editing.set(true);
    this.error.set('');
    this.notice.set('');
  }

  cancelEdit(): void { this.editing.set(false); }

  save(): void {
    this.error.set('');
    if (this.form.invalid || this.saving() || !this.canManage()) {
      this.form.markAllAsTouched();
      return;
    }
    const selected = this.selected();
    const values = this.form.getRawValue();
    const input = { name: values.name.trim(), document: values.document.trim() || null,
      instructions: values.instructions.trim() || null };
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
        this.error.set(error.status === 400
          ? 'Confira o nome, documento e instruções informados.'
          : 'Não foi possível salvar o cliente. Verifique a conexão e tente novamente.');
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
    if (this.page() * 20 >= this.totalCount() || this.loading()) return;
    this.selected.set(null);
    this.locations.set([]);
    this.page.update(value => value + 1);
    this.load();
  }

  trackCustomer(_: number, customer: Customer): string { return customer.id; }

  loadLocations(customerId: string): void {
    this.locations.set([]);
    this.locationError.set('');
    this.locationsLoading.set(true);
    this.api.locations(customerId).pipe(finalize(() => this.locationsLoading.set(false))).subscribe({
      next: locations => this.locations.set(locations),
      error: () => this.locationError.set('Não foi possível carregar os locais deste cliente.'),
    });
  }

  startCreateLocation(): void {
    if (!this.canManage() || !this.selected()) return;
    this.editingLocationId.set('new');
    this.locationForm.reset({ name: '', address: '', instructions: '' });
    this.locationError.set('');
    this.locationNotice.set('');
  }

  startEditLocation(location: CustomerLocation): void {
    if (!this.canManage()) return;
    this.editingLocationId.set(location.id);
    this.locationForm.reset({ name: location.name, address: location.address, instructions: location.instructions ?? '' });
    this.locationError.set('');
    this.locationNotice.set('');
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
    const input = { name: values.name.trim(), address: values.address.trim(), instructions: values.instructions.trim() || null };
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
