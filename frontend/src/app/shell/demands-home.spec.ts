import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AuthService } from '../core/auth.service';
import { apiInterceptor } from '../core/api.interceptor';
import { DemandsHome } from './demands-home';

describe('DemandsHome', () => {
  let fixture: ComponentFixture<DemandsHome>;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DemandsHome],
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([apiInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: { getCsrfToken: () => of('csrf-value'), expireSession: () => undefined } },
      ],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(DemandsHome);
    fixture.detectChanges();
    http.expectOne(request => request.url === '/api/demands' && request.params.get('pageSize') === '12').flush({ items: [{
      id: 'demand-a', referenceNumber: 42, customerId: 'customer-a', customerName: 'Cliente Alfa', locationId: 'location-a', locationName: 'Obra Centro',
      description: 'Revisar bomba hidráulica', origin: 'WhatsApp', priority: 'Alta', status: 'Recebida', ticketReference: null,
      purchaseOrderReference: null, assignedToUserId: null, assignedToDisplayName: null, createdAtUtc: '2026-11-17T12:00:00Z',
    }], page: 1, pageSize: 12, totalCount: 1 });
    http.expectOne(request => request.url === '/api/customers' && request.params.get('pageSize') === '100').flush({ items: [], page: 1, pageSize: 100, totalCount: 0 });
    http.expectOne('/api/users/eligible-assignees').flush([]);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  it('switches between cards and table and opens persisted demand details in a side panel', () => {
    expect(fixture.nativeElement.querySelector('.demand-card')).not.toBeNull();
    const tableButton = fixture.nativeElement.querySelector('.view-toggle button:last-child') as HTMLButtonElement;
    tableButton.click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.demand-table')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Cliente Alfa');

    const demandButton = fixture.nativeElement.querySelector('.demand-table-name') as HTMLButtonElement;
    demandButton.click();
    const request = http.expectOne('/api/demands/demand-a');
    request.flush({
      id: 'demand-a', referenceNumber: 42, customerId: 'customer-a', customerName: 'Cliente Alfa', locationId: 'location-a', locationName: 'Obra Centro',
      description: 'Revisar bomba hidráulica', origin: 'WhatsApp', priority: 'Alta', status: 'Recebida', ticketReference: null,
      purchaseOrderReference: null, assignedToUserId: null, assignedToDisplayName: null, createdByUserId: 'user-a',
      createdByDisplayName: 'Gestora', createdAtUtc: '2026-11-17T12:00:00Z', version: 2, activities: [{ id: 'activity-a', authorUserId: 'user-a',
        authorDisplayName: 'Gestora', eventType: 'DemandCreated', description: 'Demanda criada.', occurredAtUtc: '2026-11-17T12:00:00Z' }],
    });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.detail-drawer[open]')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Demanda criada.');
  });

  it('opens the existing demand in edit mode and returns to its refreshed detail after saving', () => {
    (fixture.nativeElement.querySelector('.demand-card-main') as HTMLButtonElement).click();
    http.expectOne('/api/demands/demand-a').flush({
      id: 'demand-a', referenceNumber: 42, customerId: 'customer-a', customerName: 'Cliente Alfa', locationId: 'location-a',
      locationName: 'Obra Centro', description: 'Revisar bomba hidráulica', origin: 'WhatsApp', priority: 'Alta', status: 'Recebida',
      ticketReference: null, purchaseOrderReference: null, assignedToUserId: null, assignedToDisplayName: null,
      createdByUserId: 'user-a', createdByDisplayName: 'Gestora', createdAtUtc: '2026-11-17T12:00:00Z', version: 2, activities: [],
    });
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('.detail-edit-button') as HTMLButtonElement).click();
    fixture.detectChanges();
    http.expectOne(request => request.url === '/api/customers' && request.params.get('pageSize') === '100')
      .flush({ items: [], page: 1, pageSize: 100, totalCount: 0 });
    http.expectOne('/api/users/eligible-assignees').flush([]);
    http.expectOne('/api/customers/customer-a/locations').flush([{
      id: 'location-a', customerId: 'customer-a', name: 'Obra Centro', address: 'Rua Central, 10', instructions: null,
      createdAtUtc: '2026-11-17T12:00:00Z', updatedAtUtc: '2026-11-17T12:00:00Z',
    }]);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#new-demand-title')?.textContent).toContain('Editar demanda');
    const description = fixture.nativeElement.querySelector('#description') as HTMLTextAreaElement;
    expect(description.value).toBe('Revisar bomba hidráulica');
    description.value = 'Bomba revisada';
    description.dispatchEvent(new Event('input', { bubbles: true }));
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('.form-actions .primary-button') as HTMLButtonElement).click();
    const update = http.expectOne('/api/demands/demand-a');
    expect(update.request.method).toBe('PUT');
    expect(update.request.body.version).toBe(2);
    update.flush({
      id: 'demand-a', referenceNumber: 42, customerId: 'customer-a', locationId: 'location-a',
      description: 'Bomba revisada', origin: 'WhatsApp', priority: 'Alta', status: 'Recebida', ticketReference: null,
      purchaseOrderReference: null, assignedToUserId: null, version: 3,
    });
    http.expectOne(request => request.url === '/api/demands' && request.params.get('page') === '1').flush({
      items: [], page: 1, pageSize: 12, totalCount: 0,
    });
    http.expectOne('/api/demands/demand-a').flush({
      id: 'demand-a', referenceNumber: 42, customerId: 'customer-a', customerName: 'Cliente Alfa', locationId: 'location-a',
      locationName: 'Obra Centro', description: 'Bomba revisada', origin: 'WhatsApp', priority: 'Alta', status: 'Recebida',
      ticketReference: null, purchaseOrderReference: null, assignedToUserId: null, assignedToDisplayName: null,
      createdByUserId: 'user-a', createdByDisplayName: 'Gestora', createdAtUtc: '2026-11-17T12:00:00Z', version: 3, activities: [],
    });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.detail-drawer[open]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('#demand-detail-title')?.textContent).toContain('Cliente Alfa');
    expect(fixture.nativeElement.textContent).toContain('Bomba revisada');
    expect(fixture.nativeElement.textContent).toContain('atualizada');
  });

  it('opens the adjusted demand form in a side panel and closes it without navigating away', () => {
    (fixture.nativeElement.querySelector('.page-heading .primary-button') as HTMLButtonElement).click();
    fixture.detectChanges();
    http.expectOne(request => request.url === '/api/customers' && request.params.get('pageSize') === '100')
      .flush({ items: [], page: 1, pageSize: 100, totalCount: 0 });
    http.expectOne('/api/users/eligible-assignees').flush([]);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.create-drawer[open]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('#new-demand-title')?.textContent).toContain('Nova demanda');

    (fixture.nativeElement.querySelector('.create-drawer .dialog-close') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.create-drawer[open]')).toBeNull();
  });

  it('renders 2, 5 and 12 cards with long descriptions clamped in the card layout', () => {
    const demand = fixture.componentInstance.demands()[0];
    demand.description = 'Texto longo de descrição com equipamento, sintomas, local de atendimento e detalhes adicionais para evitar que os cards cresçam em alturas diferentes.';
    for (const count of [2, 5, 12]) {
      fixture.componentInstance.demands.set(Array.from({ length: count }, (_, index) => ({
        ...demand, id: `demand-${index}`, referenceNumber: index + 1,
      })));
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelectorAll('.demand-card').length).toBe(count);
      expect(fixture.nativeElement.querySelector('.demand-description')?.textContent).toContain('Texto longo');
    }
  });

  it('shows distinct empty states for an empty organization and filters with no matches', () => {
    fixture.componentInstance.demands.set([]);
    fixture.componentInstance.totalCount.set(0);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.empty-state h3')?.textContent).toContain('Nenhuma demanda registrada');
    expect(fixture.nativeElement.querySelector('.empty-state .primary-button')).not.toBeNull();

    fixture.componentInstance.status.setValue('Recebida');
    http.expectOne(request => request.url === '/api/demands' && request.params.get('status') === 'Recebida')
      .flush({ items: [], page: 1, pageSize: 12, totalCount: 0 });
    expect(fixture.componentInstance.hasFilters()).toBe(true);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.empty-state h3')?.textContent).toContain('Nenhuma demanda encontrada');
    expect(fixture.nativeElement.querySelector('.empty-state .secondary-button')?.textContent).toContain('Limpar filtros');
  });
});
