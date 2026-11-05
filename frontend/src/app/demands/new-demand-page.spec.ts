import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AuthService } from '../core/auth.service';
import { apiInterceptor } from '../core/api.interceptor';
import { NewDemandPage } from './new-demand-page';

describe('NewDemandPage', () => {
  let fixture: ComponentFixture<NewDemandPage>;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [NewDemandPage],
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([apiInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: { getCsrfToken: () => of('csrf-value'), expireSession: () => undefined } },
      ],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(NewDemandPage);
    fixture.detectChanges();
    http.expectOne('/api/customers?page=1&pageSize=100').flush({ items: [
      { id: 'customer-a', name: 'Cliente Alfa', document: null, instructions: null,
        createdAtUtc: '2026-11-05T12:00:00Z', updatedAtUtc: '2026-11-05T12:00:00Z' },
    ], page: 1, pageSize: 100, totalCount: 1 });
    http.expectOne('/api/users/eligible-assignees').flush([{ id: 'user-a', displayName: 'Gestora' }]);
  });

  afterEach(() => http.verify());

  it('filters locations by the selected customer and submits one CSRF-protected request', () => {
    const page = fixture.componentInstance;
    page.form.controls.customerId.setValue('customer-a');
    page.onCustomerChange();
    http.expectOne('/api/customers/customer-a/locations').flush([{
      id: 'location-a', customerId: 'customer-a', name: 'Unidade Central', address: 'Rua das Flores, 100',
      instructions: null, createdAtUtc: '2026-11-05T12:00:00Z', updatedAtUtc: '2026-11-05T12:00:00Z',
    }]);
    page.form.patchValue({
      locationId: 'location-a', description: 'Manutenção solicitada pelo cliente', origin: 'WhatsApp',
      assignedToUserId: '', ticketReference: '', purchaseOrderReference: '', priority: 'Média',
    });
    page.submit();
    page.submit();
    const request = http.expectOne('/api/demands');
    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('X-CSRF-TOKEN')).toBe('csrf-value');
    expect(request.request.body).toEqual({
      customerId: 'customer-a', locationId: 'location-a', description: 'Manutenção solicitada pelo cliente',
      origin: 'WhatsApp', assignedToUserId: null, ticketReference: null, purchaseOrderReference: null, priority: 'Média',
    });
    request.flush({ id: 'demand-a', customerId: 'customer-a', locationId: 'location-a',
      description: 'Manutenção solicitada pelo cliente', origin: 'WhatsApp', priority: 'Média', status: 'Recebida',
      ticketReference: null, purchaseOrderReference: null, assignedToUserId: null,
      createdAtUtc: '2026-11-05T12:00:00Z' });
    expect(page.created()?.id).toBe('demand-a');
    expect(page.created()?.status).toBe('Recebida');
  });

  it('clears the local when changing customers and preserves entered data on API validation errors', () => {
    const page = fixture.componentInstance;
    page.form.patchValue({ customerId: 'customer-a', locationId: 'location-old' });
    page.onCustomerChange();
    expect(page.form.controls.locationId.value).toBe('');
    http.expectOne('/api/customers/customer-a/locations').flush([]);
    page.form.patchValue({ locationId: 'location-old', description: 'Pedido original', origin: 'Ligação' });
    page.submit();
    const request = http.expectOne('/api/demands');
    request.flush({ errors: { LocationId: ['O local não pertence ao cliente informado.'] } },
      { status: 400, statusText: 'Bad Request' });
    expect(page.form.controls.description.value).toBe('Pedido original');
    expect(page.fieldError('locationId')).toBe('O local não pertence ao cliente informado.');
    expect(page.submitError()).toContain('Seus dados continuam preenchidos');
  });
});
