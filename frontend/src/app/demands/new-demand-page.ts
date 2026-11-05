import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
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
  private readonly formBuilder = inject(FormBuilder);
  private readonly customersApi = inject(CustomerApi);
  private readonly demandApi = inject(DemandApi);
  readonly form = this.formBuilder.nonNullable.group({
    customerId: ['', Validators.required],
    locationId: ['', Validators.required],
    description: ['', [Validators.required, Validators.maxLength(4000)]],
    origin: ['', Validators.required],
    assignedToUserId: [''],
    ticketReference: ['', Validators.maxLength(100)],
    purchaseOrderReference: ['', Validators.maxLength(100)],
    priority: [''],
  });
  readonly customerSearch = this.formBuilder.nonNullable.control('');
  readonly customers = signal<Customer[]>([]);
  readonly locations = signal<CustomerLocation[]>([]);
  readonly assignees = signal<EligibleAssignee[]>([]);
  readonly searchingCustomers = signal(false);
  readonly loadingLocations = signal(false);
  readonly loadingAssignees = signal(false);
  readonly submitting = signal(false);
  readonly pageError = signal('');
  readonly submitError = signal('');
  readonly fieldErrors = signal<Record<string, string[]>>({});
  readonly created = signal<CreatedDemand | null>(null);

  ngOnInit(): void {
    this.searchCustomers();
    this.loadAssignees();
  }

  searchCustomers(): void {
    this.pageError.set('');
    this.searchingCustomers.set(true);
    this.customersApi.search(this.customerSearch.value, 1, 100)
      .pipe(finalize(() => this.searchingCustomers.set(false)))
      .subscribe({
        next: result => {
          this.customers.set(result.items);
          if (!result.items.some(customer => customer.id === this.form.controls.customerId.value)) {
            this.form.controls.customerId.setValue('');
            this.form.controls.locationId.setValue('');
            this.locations.set([]);
          }
        },
        error: () => this.pageError.set('Não foi possível carregar clientes. Tente novamente.'),
      });
  }

  loadAssignees(): void {
    this.loadingAssignees.set(true);
    this.demandApi.eligibleAssignees().pipe(finalize(() => this.loadingAssignees.set(false))).subscribe({
      next: users => this.assignees.set(users),
      error: () => this.pageError.set('Não foi possível carregar os responsáveis elegíveis.'),
    });
  }

  onCustomerChange(): void {
    const customerId = this.form.controls.customerId.value;
    this.form.controls.locationId.setValue('');
    this.locations.set([]);
    this.submitError.set('');
    if (!customerId) return;
    this.loadingLocations.set(true);
    this.customersApi.locations(customerId).pipe(finalize(() => this.loadingLocations.set(false))).subscribe({
      next: locations => this.locations.set(locations),
      error: () => this.pageError.set('Não foi possível carregar os locais deste cliente.'),
    });
  }

  submit(): void {
    this.submitError.set('');
    this.fieldErrors.set({});
    if (this.submitting() || this.form.invalid) {
      this.form.markAllAsTouched();
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
      next: demand => this.created.set(demand),
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
