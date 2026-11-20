import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { apiInterceptor } from '../core/api.interceptor';
import { InitialPasswordChangePage } from './initial-password-change-page';

describe('InitialPasswordChangePage', () => {
  let fixture: ComponentFixture<InitialPasswordChangePage>;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [InitialPasswordChangePage],
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([apiInterceptor])),
        provideHttpClientTesting(),
      ],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(InitialPasswordChangePage);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  it('requires a matching confirmation before calling the password endpoint', () => {
    const page = fixture.componentInstance;
    page.form.setValue({ currentPassword: 'Temporary-123!', newPassword: 'Changed-456!', confirmPassword: 'Different-456!' });
    page.submit();
    fixture.detectChanges();

    expect(page.form.hasError('passwordMismatch')).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('As senhas informadas são diferentes.');
    http.expectNone('/api/auth/csrf');
  });

  it('posts the current and new password through the CSRF-protected API', () => {
    const page = fixture.componentInstance;
    page.form.setValue({ currentPassword: 'Temporary-123!', newPassword: 'Changed-456!', confirmPassword: 'Changed-456!' });
    page.submit();

    http.expectOne('/api/auth/csrf').flush({ requestToken: 'csrf-value' });
    const change = http.expectOne('/api/auth/change-password');
    expect(change.request.method).toBe('POST');
    expect(change.request.headers.get('X-CSRF-TOKEN')).toBe('csrf-value');
    expect(change.request.body).toEqual({ currentPassword: 'Temporary-123!', newPassword: 'Changed-456!' });
    change.flush(null);
  });
});
