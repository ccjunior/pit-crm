import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AuthService, AuthSession } from '../core/auth.service';
import { apiInterceptor } from '../core/api.interceptor';
import { CustomersPage } from './customers-page';

describe('CustomersPage', () => {
  let fixture: ComponentFixture<CustomersPage>;
  let http: HttpTestingController;
  let session: ReturnType<typeof signal<AuthSession | null>>;

  beforeEach(async () => {
    session = signal<AuthSession | null>({
      id: 'user-a', displayName: 'Gestora', email: 'gestora@example.invalid',
      organization: { id: 'org-a', name: 'Empresa A' }, roles: ['Gestor'], mustChangePassword: false,
    });
    await TestBed.configureTestingModule({
      imports: [CustomersPage],
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([apiInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: {
          session,
          expireSession: () => session.set(null),
          getCsrfToken: () => of('csrf-value'),
        } },
      ],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(CustomersPage);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  it('loads customers and presents an empty result clearly', () => {
    http.expectOne(request => request.url === '/api/customers' && request.params.get('pageSize') === '12')
      .flush({ items: [], page: 1, pageSize: 12, totalCount: 0 });
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Nenhum cliente cadastrado ainda');
    expect((fixture.nativeElement as HTMLElement).querySelector('button.primary-button')?.textContent).toContain('Novo cliente');
  });

  it('creates a client with CSRF protection and updates the list', () => {
    http.expectOne(request => request.url === '/api/customers' && request.params.get('page') === '1' && request.params.get('pageSize') === '12')
      .flush({ items: [], page: 1, pageSize: 12, totalCount: 0 });
    const page = fixture.componentInstance;
    page.startCreate();
    page.form.setValue({ name: 'Cliente Alfa', document: '', instructions: '', postalCode: '', address: '', locality: '', city: '', state: '' });
    page.save();
    const create = http.expectOne('/api/customers');
    expect(create.request.method).toBe('POST');
    expect(create.request.headers.get('X-CSRF-TOKEN')).toBe('csrf-value');
    create.flush({ id: 'customer-a', name: 'Cliente Alfa', document: null, instructions: null, postalCode: null, address: null, locality: null, city: null, state: null,
      createdAtUtc: '2026-11-05T12:00:00Z', updatedAtUtc: '2026-11-05T12:00:00Z' });
    http.expectOne('/api/customers/customer-a/locations').flush([]);
    http.expectOne(request => request.url === '/api/customers' && request.params.get('page') === '1' && request.params.get('pageSize') === '12').flush({
      items: [{ id: 'customer-a', name: 'Cliente Alfa', document: null, instructions: null, postalCode: null, address: null, locality: null, city: null, state: null,
        createdAtUtc: '2026-11-05T12:00:00Z', updatedAtUtc: '2026-11-05T12:00:00Z' }],
      page: 1, pageSize: 12, totalCount: 1,
    });
    expect(page.notice()).toBe('Cliente cadastrado.');
    expect(page.customers().length).toBe(1);
  });

  it('hides customer write controls from an operator', () => {
    session.set({ id: 'user-o', displayName: 'Operador', email: 'op@example.invalid',
      organization: { id: 'org-a', name: 'Empresa A' }, roles: ['Operador'], mustChangePassword: false });
    const customer = { id: 'customer-a', name: 'Cliente Alfa', document: null, instructions: null, postalCode: null, address: null, locality: null, city: null, state: null,
      createdAtUtc: '2026-11-05T12:00:00Z', updatedAtUtc: '2026-11-05T12:00:00Z' };
    http.expectOne(request => request.url === '/api/customers' && request.params.get('page') === '1' && request.params.get('pageSize') === '12')
      .flush({ items: [customer], page: 1, pageSize: 12, totalCount: 1 });
    fixture.componentInstance.select(customer);
    http.expectOne('/api/customers/customer-a/contacts?allScopes=true').flush([]);
    http.expectOne('/api/customers/customer-a/locations').flush([{
      id: 'location-a', customerId: 'customer-a', name: 'Unidade Central', address: 'Rua das Flores, 100',
      neighborhood: null, city: null, mapUrl: null, status: 'Active', instructions: null, responsibleContactId: null,
      createdAtUtc: '2026-11-05T12:00:00Z', updatedAtUtc: '2026-11-05T12:00:00Z',
    }, {
      id: 'location-b', customerId: 'customer-a', name: 'Unidade Norte', address: 'Avenida Brasil, 20',
      neighborhood: null, city: null, mapUrl: null, status: 'Active', instructions: 'Recepção lateral', responsibleContactId: null,
      createdAtUtc: '2026-11-05T12:00:00Z', updatedAtUtc: '2026-11-05T12:00:00Z',
    }]);
    http.expectOne('/api/customers/customer-a/contacts?allScopes=true').flush([]);
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('.customer-detail-tabs button:nth-child(2)') as HTMLButtonElement).click();
    fixture.detectChanges();
    const rendered = fixture.nativeElement as HTMLElement;
    expect(rendered.querySelector('button.primary-button')).toBeNull();
    expect(rendered.querySelectorAll('.detail-heading .secondary-button')).toHaveLength(0);
    expect(rendered.querySelectorAll('.location-item')).toHaveLength(2);
    expect(rendered.querySelectorAll('.location-item .text-button')).toHaveLength(0);
  });

  it('lists and creates locations only within the selected customer', () => {
    const customer = { id: 'customer-a', name: 'Cliente Alfa', document: null, instructions: null, postalCode: null, address: null, locality: null, city: null, state: null,
      createdAtUtc: '2026-11-05T12:00:00Z', updatedAtUtc: '2026-11-05T12:00:00Z' };
    http.expectOne(request => request.url === '/api/customers' && request.params.get('page') === '1' && request.params.get('pageSize') === '12')
      .flush({ items: [customer], page: 1, pageSize: 12, totalCount: 1 });
    const page = fixture.componentInstance;
    page.select(customer);
    http.match('/api/customers/customer-a/contacts?allScopes=true').forEach(request => request.flush([]));
    http.expectOne('/api/customers/customer-a/locations').flush([]);
    http.expectOne('/api/customers/customer-a/contacts?allScopes=true').flush([]);
    page.startCreateLocation();
    page.locationForm.setValue({ name: 'Unidade Central', address: 'Rua das Flores, 100', neighborhood: '', city: '', mapUrl: '', status: 'Active', instructions: '', responsibleContactId: '' });
    page.saveLocation();
    const create = http.expectOne('/api/customers/customer-a/locations');
    expect(create.request.method).toBe('POST');
    expect(create.request.headers.get('X-CSRF-TOKEN')).toBe('csrf-value');
    create.flush({ id: 'location-a', customerId: 'customer-a', name: 'Unidade Central',
      address: 'Rua das Flores, 100', neighborhood: null, city: null, mapUrl: null, status: 'Active', instructions: null,
      responsibleContactId: null,
      createdAtUtc: '2026-11-05T12:00:00Z', updatedAtUtc: '2026-11-05T12:00:00Z' });
    http.expectOne('/api/customers/customer-a/locations').flush([{
      id: 'location-a', customerId: 'customer-a', name: 'Unidade Central', address: 'Rua das Flores, 100',
      neighborhood: null, city: null, mapUrl: null, status: 'Active', instructions: null, responsibleContactId: null,
      createdAtUtc: '2026-11-05T12:00:00Z', updatedAtUtc: '2026-11-05T12:00:00Z',
    }, {
      id: 'location-b', customerId: 'customer-a', name: 'Unidade Norte', address: 'Avenida Brasil, 20',
      neighborhood: null, city: null, mapUrl: null, status: 'Active', instructions: 'Recepção lateral', responsibleContactId: null,
      createdAtUtc: '2026-11-05T12:00:00Z', updatedAtUtc: '2026-11-05T12:00:00Z',
    }]);
    http.match('/api/customers/customer-a/contacts?allScopes=true').forEach(request => request.flush([]));
    expect(page.locations().map(location => location.id)).toEqual(['location-a', 'location-b']);
    expect(page.locationNotice()).toBe('Local cadastrado.');
  });
});
