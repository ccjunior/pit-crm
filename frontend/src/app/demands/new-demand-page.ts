import { HttpErrorResponse } from '@angular/common/http';
import { Component, EventEmitter, Input, OnInit, Output, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { Customer, CustomerApi, CustomerLocation } from '../customers/customer-api';
import { CreatedDemand, DemandApi, EligibleAssignee } from './demand-api';

@Component({
  selector: 'app-new-demand-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './new-demand-page.html',
  styleUrl: './new-demand-page.css',
})
export class NewDemandPage implements OnInit {
  @Output() readonly cancelled = new EventEmitter<void>();
  @Output() readonly saved = new EventEmitter<CreatedDemand>();
  @Input() embedded = false;
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
    ticketReference: ['', Validators.maxLength(100)],
    purchaseOrderReference: ['', Validators.maxLength(100)],
    priority: [''],
  });
  readonly customerSearch = this.formBuilder.nonNullable.control('');
  readonly customers = signal<Customer[]>([]);
  readonly totalCustomerMatches = signal(0);
  readonly locations = signal<CustomerLocation[]>([]);
  readonly assignees = signal<EligibleAssignee[]>([]);
  readonly searchingCustomers = signal(false);
  readonly loadingLocations = signal(false);
  readonly loadingAssignees = signal(false);
  readonly submitting = signal(false);
  readonly submitted = signal(false);
  readonly customerPickerOpen = signal(false);
  readonly activeCustomerIndex = signal(0);
  readonly pageError = signal('');
  readonly submitError = signal('');
  readonly fieldErrors = signal<Record<string, string[]>>({});
  readonly created = signal<CreatedDemand | null>(null);
  private customerSearchRequestId = 0;
  private locationRequestId = 0;

  ngOnInit(): void {
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
          if (!result.items.some(customer => customer.id === this.form.controls.customerId.value)) {
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

  onCustomerChange(): void {
    const requestId = ++this.locationRequestId;
    const customerId = this.form.controls.customerId.value;
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
        if (locations.length) this.form.controls.locationId.enable();
        if (locations.length === 1) this.form.controls.locationId.setValue(locations[0].id);
      },
      error: () => {
        if (requestId === this.locationRequestId) this.pageError.set('Não foi possível carregar os locais deste cliente.');
      },
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
    if (this.submitting() || this.form.invalid || !this.form.controls.locationId.value) {
      this.form.markAllAsTouched();
      this.customerPickerOpen.set(false);
      return;
    }
    const values = this.form.getRawValue();
    this.submitting.set(true);
    this.demandApi.create({
      customerId: values.customerId,
      locationId: values.locationId,
      description: values.description.trim(),
      origin: values.origin,
      assignedToUserId: values.assignedToUserId || null,
      ticketReference: values.ticketReference.trim() || null,
      purchaseOrderReference: values.purchaseOrderReference.trim() || null,
      priority: values.priority || null,
    }).pipe(finalize(() => this.submitting.set(false))).subscribe({
      next: demand => {
        this.created.set(demand);
        if (this.embedded) this.saved.emit(demand);
      },
      error: (error: HttpErrorResponse) => {
        const apiErrors = (error.error as { errors?: Record<string, string[]> } | null)?.errors;
        if (apiErrors) this.fieldErrors.set(apiErrors);
        this.submitError.set(error.status === 400
          ? 'Revise os campos destacados e confirme que o local pertence ao cliente selecionado. Seus dados continuam preenchidos.'
          : 'Não foi possível confirmar a gravação. Seus dados foram mantidos; confira as demandas antes de enviar outra vez.');
      },
    });
  }

  fieldError(name: string): string | null {
    const match = Object.entries(this.fieldErrors()).find(([key]) => key.toLowerCase() === name.toLowerCase());
    return match?.[1][0] ?? null;
  }
}
