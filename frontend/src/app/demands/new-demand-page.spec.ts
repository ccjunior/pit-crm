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
    http.expectOne(request => request.url === '/api/customers'
      && request.params.get('page') === '1' && request.params.get('pageSize') === '100').flush({ items: [
      { id: 'customer-a', name: 'Cliente Alfa', document: null, instructions: null, address: null, locality: null, city: null, state: null,
        createdAtUtc: '2026-11-05T12:00:00Z', updatedAtUtc: '2026-11-05T12:00:00Z' },
    ], page: 1, pageSize: 100, totalCount: 1 });
    http.expectOne('/api/users/eligible-assignees').flush([{ id: 'user-a', displayName: 'Gestora' }]);
  });

  afterEach(() => http.verify());

  it('waits for blur or submit before showing required-field errors', () => {
    expect(fixture.nativeElement.querySelector('.field-error')).toBeNull();
    const customerInput = fixture.nativeElement.querySelector('#customer-search') as HTMLInputElement;
    customerInput.dispatchEvent(new Event('blur'));
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#customer-error')?.textContent).toContain('Selecione um cliente.');
    fixture.componentInstance.submit();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#description-error')?.textContent).toContain('obrigatória');
    expect(fixture.nativeElement.querySelector('.progress-checklist li.complete')).toBeNull();
  });

  it('searches and selects a customer in one combobox and selects its only location', () => {
    const input = fixture.nativeElement.querySelector('#customer-search') as HTMLInputElement;
    input.value = 'Alfa';
    input.dispatchEvent(new Event('input', { bubbles: true }));
    fixture.detectChanges();

    http.expectOne(request => request.url === '/api/customers' && request.params.get('q') === 'Alfa')
      .flush({ items: [{ id: 'customer-a', name: 'Cliente Alfa', document: null }], page: 1, pageSize: 100, totalCount: 1 });
    fixture.detectChanges();
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true, cancelable: true }));
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true }));
    fixture.detectChanges();

    expect(fixture.componentInstance.form.controls.customerId.value).toBe('customer-a');
    expect((fixture.nativeElement.querySelector('#customer-search') as HTMLInputElement).value).toBe('Cliente Alfa');
    expect(fixture.nativeElement.querySelector('#location-id').disabled).toBe(true);
    http.expectOne('/api/customers/customer-a/locations').flush([{
      id: 'location-a', customerId: 'customer-a', name: 'Unidade Central', address: 'Rua das Flores, 100',
      instructions: null, createdAtUtc: '2026-11-05T12:00:00Z', updatedAtUtc: '2026-11-05T12:00:00Z',
    }]);
    fixture.detectChanges();

    expect(fixture.componentInstance.form.controls.locationId.value).toBe('location-a');
    expect(fixture.nativeElement.querySelector('#location-id').disabled).toBe(false);
    fixture.componentInstance.form.patchValue({ description: 'Ar condicionado não refrigera', origin: 'WhatsApp' });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('.progress-checklist li.complete').length).toBe(4);
  });

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
      assignedToUserId: '', priority: 'Média',
    });
    page.addExternalReference();
    page.updateExternalReference(0, 'type', 'Chamado/OS');
    page.updateExternalReference(0, 'value', 'CH-15');
    page.addExternalReference();
    page.updateExternalReference(1, 'type', 'Código do Cliente');
    page.updateExternalReference(1, 'value', 'REF-9');
    page.submit();
    page.submit();
    const request = http.expectOne('/api/demands');
    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('X-CSRF-TOKEN')).toBe('csrf-value');
    expect(request.request.body).toEqual({
      customerId: 'customer-a', locationId: 'location-a', description: 'Manutenção solicitada pelo cliente',
      origin: 'WhatsApp', assignedToUserId: null,
      externalReferences: [{ type: 'Chamado/OS', value: 'CH-15' }, { type: 'Código do Cliente', value: 'REF-9' }], priority: 'Média',
    });
    request.flush({ id: 'demand-a', referenceNumber: 42, customerId: 'customer-a', locationId: 'location-a',
      description: 'Manutenção solicitada pelo cliente', origin: 'WhatsApp', priority: 'Média', status: 'Recebida',
      externalReferences: [{ type: 'Chamado/OS', value: 'CH-15' }, { type: 'Código do Cliente', value: 'REF-9' }], assignedToUserId: null,
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

  it('requires both type and value when an external reference row is partially filled', () => {
    const page = fixture.componentInstance;
    page.form.patchValue({ customerId: 'customer-a' });
    page.onCustomerChange();
    http.expectOne('/api/customers/customer-a/locations').flush([{
      id: 'location-a', customerId: 'customer-a', name: 'Unidade Central', address: 'Rua das Flores, 100',
      instructions: null, createdAtUtc: '2026-11-05T12:00:00Z', updatedAtUtc: '2026-11-05T12:00:00Z',
    }]);
    page.form.patchValue({ locationId: 'location-a', description: 'Manutenção solicitada', origin: 'WhatsApp' });
    page.addExternalReference();
    page.updateExternalReference(0, 'type', 'Chamado/OS');
    page.submit();
    fixture.detectChanges();

    expect(page.externalReferenceErrors()[0]).toContain('Preencha o tipo e o valor');
    expect(fixture.nativeElement.textContent).toContain('Preencha o tipo e o valor');
  });

  it('prefills a demand, sends its version and keeps edits when the version conflicts', () => {
    const page = fixture.componentInstance;
    page.existingDemand = {
      id: 'demand-a', referenceNumber: 42, customerId: 'customer-a', customerName: 'Cliente Alfa',
      locationId: 'location-a', locationName: 'Unidade Central', description: 'Descrição atual', origin: 'WhatsApp',
      priority: 'Alta', status: 'Recebida', externalReferences: [{ type: 'Chamado/OS', value: 'CH-12' }, { type: 'Ordem de Compra', value: 'OC-8' }],
      assignedToUserId: 'user-a', assignedToDisplayName: 'Gestora', createdAtUtc: '2026-11-05T12:00:00Z',
      createdByDisplayName: 'Gestora', activities: [], version: 3,
    };
    page.ngOnInit();
    http.expectOne(request => request.url === '/api/customers' && request.params.get('pageSize') === '100')
      .flush({ items: [], page: 1, pageSize: 100, totalCount: 0 });
    http.expectOne('/api/users/eligible-assignees').flush([{ id: 'user-a', displayName: 'Gestora' }]);
    http.expectOne('/api/customers/customer-a/locations').flush([{
      id: 'location-a', customerId: 'customer-a', name: 'Unidade Central', address: 'Rua das Flores, 100',
      instructions: null, createdAtUtc: '2026-11-05T12:00:00Z', updatedAtUtc: '2026-11-05T12:00:00Z',
    }]);
    expect(page.form.getRawValue()).toMatchObject({
      customerId: 'customer-a', locationId: 'location-a', description: 'Descrição atual', origin: 'WhatsApp',
      assignedToUserId: 'user-a', priority: 'Alta',
    });
    expect(page.externalReferences()).toEqual([{ type: 'Chamado/OS', value: 'CH-12' }, { type: 'Ordem de Compra', value: 'OC-8' }]);
    page.form.controls.description.setValue('Descrição editada');
    page.submit();
    const request = http.expectOne('/api/demands/demand-a');
    expect(request.request.method).toBe('PUT');
    expect(request.request.headers.get('X-CSRF-TOKEN')).toBe('csrf-value');
    expect(request.request.body).toEqual({
      customerId: 'customer-a', locationId: 'location-a', description: 'Descrição editada', origin: 'WhatsApp',
      assignedToUserId: 'user-a', externalReferences: [{ type: 'Chamado/OS', value: 'CH-12' }, { type: 'Ordem de Compra', value: 'OC-8' }],
      priority: 'Alta', version: 3,
    });
    request.flush({ error: 'A demanda foi alterada por outra pessoa.' }, { status: 409, statusText: 'Conflict' });
    expect(page.form.controls.description.value).toBe('Descrição editada');
    expect(page.versionConflict()).toBe(true);
    expect(page.submitError()).toContain('alterada');
  });

  it('requires an explicit reload after a conflict and uses the latest version', () => {
    const page = fixture.componentInstance;
    page.existingDemand = {
      id: 'demand-a', referenceNumber: 42, customerId: 'customer-a', customerName: 'Cliente Alfa',
      locationId: 'location-a', locationName: 'Unidade Central', description: 'Antes', origin: 'Ligação',
      priority: null, status: 'Recebida', externalReferences: [],
      assignedToUserId: null, assignedToDisplayName: null, createdAtUtc: '2026-11-05T12:00:00Z',
      createdByDisplayName: 'Gestora', activities: [], version: 3,
    };
    page.ngOnInit();
    http.expectOne(request => request.url === '/api/customers' && request.params.get('pageSize') === '100')
      .flush({ items: [], page: 1, pageSize: 100, totalCount: 0 });
    http.expectOne('/api/users/eligible-assignees').flush([]);
    http.expectOne('/api/customers/customer-a/locations').flush([{
      id: 'location-a', customerId: 'customer-a', name: 'Unidade Central', address: 'Rua das Flores, 100',
      instructions: null, createdAtUtc: '2026-11-05T12:00:00Z', updatedAtUtc: '2026-11-05T12:00:00Z',
    }]);
    page.form.controls.description.setValue('Minha versão');
    page.versionConflict.set(true);
    page.reloadCurrentDemand();
    const reload = http.expectOne('/api/demands/demand-a');
    expect(reload.request.method).toBe('GET');
    reload.flush({
      id: 'demand-a', referenceNumber: 42, customerId: 'customer-a', customerName: 'Cliente Alfa',
      locationId: 'location-a', locationName: 'Unidade Central', description: 'Versão mais recente', origin: 'E-mail',
      priority: 'Baixa', status: 'Recebida', externalReferences: [],
      assignedToUserId: null, assignedToDisplayName: null, createdAtUtc: '2026-11-05T12:00:00Z', version: 4,
    });
    http.expectOne('/api/customers/customer-a/locations').flush([{
      id: 'location-a', customerId: 'customer-a', name: 'Unidade Central', address: 'Rua das Flores, 100',
      instructions: null, createdAtUtc: '2026-11-05T12:00:00Z', updatedAtUtc: '2026-11-05T12:00:00Z',
    }]);
    expect(page.form.controls.description.value).toBe('Versão mais recente');
    expect(page.existingDemand?.version).toBe(4);
    expect(page.versionConflict()).toBe(false);
  });
});
