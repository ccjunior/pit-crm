import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../core/auth.service';

@Component({
  selector: 'app-login-page',
  imports: [ReactiveFormsModule, RouterLink],
  styleUrl: './login-page.css',
  templateUrl: './login-page.html',
})
export class LoginPage {
  private readonly formBuilder = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly form = this.formBuilder.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });
  readonly pending = signal(false);
  readonly errorMessage = signal('');

  submit(): void {
    this.errorMessage.set('');
    if (this.form.invalid || this.pending()) {
      this.form.markAllAsTouched();
      return;
    }

    const { email, password } = this.form.getRawValue();
    this.pending.set(true);
    this.auth.login(email.trim(), password).pipe(finalize(() => this.pending.set(false))).subscribe({
      next: () => void this.router.navigate(['/demandas']),
      error: (error: HttpErrorResponse) => {
        this.errorMessage.set(error.status === 0
          ? 'Não foi possível conectar ao sistema. Verifique a conexão e tente novamente.'
          : 'E-mail ou senha inválidos. Confira os dados e tente novamente.');
      },
    });
  }
}
