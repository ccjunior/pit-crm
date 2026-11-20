import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../core/auth.service';

const passwordsMatch: ValidatorFn = (control: AbstractControl): ValidationErrors | null =>
  control.get('newPassword')?.value === control.get('confirmPassword')?.value ? null : { passwordMismatch: true };

@Component({
  selector: 'app-initial-password-change-page',
  imports: [ReactiveFormsModule],
  styleUrl: './initial-password-change-page.css',
  templateUrl: './initial-password-change-page.html',
})
export class InitialPasswordChangePage {
  private readonly formBuilder = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly pending = signal(false);
  readonly errorMessage = signal('');
  readonly form = this.formBuilder.nonNullable.group({
    currentPassword: ['', Validators.required],
    newPassword: ['', [Validators.required, Validators.minLength(6)]],
    confirmPassword: ['', Validators.required],
  }, { validators: passwordsMatch });

  submit(): void {
    this.errorMessage.set('');
    if (this.form.invalid || this.pending()) {
      this.form.markAllAsTouched();
      return;
    }

    const { currentPassword, newPassword } = this.form.getRawValue();
    this.pending.set(true);
    this.auth.changeOwnPassword(currentPassword, newPassword)
      .pipe(finalize(() => this.pending.set(false)))
      .subscribe({
        next: () => void this.router.navigate(['/']),
        error: (error: HttpErrorResponse) => {
          const currentPasswordError = error.error?.errors?.CurrentPassword?.[0];
          const newPasswordError = error.error?.errors?.NewPassword?.[0];
          this.errorMessage.set(currentPasswordError || newPasswordError || (error.status === 0
            ? 'Não foi possível conectar ao sistema. Verifique a conexão e tente novamente.'
            : 'Não foi possível atualizar sua senha. Confira os dados e tente novamente.'));
        },
      });
  }
}
