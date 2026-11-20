import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AuthService } from '../core/auth.service';
import { apiInterceptor } from '../core/api.interceptor';
import { TeamPage } from './team-page';

describe('TeamPage', () => {
  let fixture: ComponentFixture<TeamPage>;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TeamPage],
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([apiInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: { getCsrfToken: () => of('csrf-value'), expireSession: () => undefined } },
      ],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(TeamPage);
    fixture.detectChanges();
    http.expectOne(request => request.url === '/api/users' && request.params.get('page') === '1')
      .flush({ items: [{ id: 'admin-a', displayName: 'Ana Administradora', email: 'ana@example.invalid', role: 'Administrador', isActive: true, mustChangePassword: false }], page: 1, pageSize: 20, totalCount: 1 });
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  it('creates an account with the selected base role and presents the temporary password for handoff', () => {
    const page = fixture.componentInstance;
    page.startCreate();
    page.form.patchValue({ displayName: 'Joana Operadora', email: 'joana@example.invalid', role: 'Operador', temporaryPassword: 'Temporary-123!' });
    page.save();

    const create = http.expectOne('/api/users');
    expect(create.request.method).toBe('POST');
    expect(create.request.headers.get('X-CSRF-TOKEN')).toBe('csrf-value');
    expect(create.request.body).toEqual({ displayName: 'Joana Operadora', email: 'joana@example.invalid', role: 'Operador', temporaryPassword: 'Temporary-123!' });
    create.flush({ id: 'user-b', displayName: 'Joana Operadora', email: 'joana@example.invalid', role: 'Operador', isActive: true, mustChangePassword: true });

    http.expectOne(request => request.url === '/api/users' && request.params.get('page') === '1')
      .flush({ items: [], page: 1, pageSize: 20, totalCount: 2 });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Entregue esta senha fora do PIT Obras');
    expect(fixture.nativeElement.textContent).toContain('Temporary-123!');
  });

  it('keeps the edit form open and explains the API conflict when the last Administrator cannot be changed', () => {
    const page = fixture.componentInstance;
    page.startEdit(page.members()[0]);
    page.form.patchValue({ role: 'Gestor' });
    page.save();

    const update = http.expectOne('/api/users/admin-a');
    expect(update.request.method).toBe('PUT');
    update.flush({ error: 'A organização precisa manter pelo menos um Administrador ativo.' }, { status: 409, statusText: 'Conflict' });
    fixture.detectChanges();
    expect(page.editorOpen()).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('A organização precisa manter pelo menos um Administrador ativo.');
  });

  it('updates an account and refreshes the organization team list', () => {
    const page = fixture.componentInstance;
    page.startEdit(page.members()[0]);
    page.form.patchValue({ displayName: 'Ana Oliveira', role: 'Gestor' });
    page.save();

    const update = http.expectOne('/api/users/admin-a');
    expect(update.request.body).toEqual({
      displayName: 'Ana Oliveira', email: 'ana@example.invalid', role: 'Gestor', isActive: true,
    });
    update.flush({ id: 'admin-a', displayName: 'Ana Oliveira', email: 'ana@example.invalid', role: 'Gestor', isActive: true, mustChangePassword: false });
    http.expectOne(request => request.url === '/api/users' && request.params.get('page') === '1')
      .flush({ items: [{ id: 'admin-a', displayName: 'Ana Oliveira', email: 'ana@example.invalid', role: 'Gestor', isActive: true, mustChangePassword: false }], page: 1, pageSize: 20, totalCount: 1 });
    fixture.detectChanges();

    expect(page.editorOpen()).toBe(false);
    expect(fixture.nativeElement.textContent).toContain('Ana Oliveira');
  });
});
