import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth.service';

@Component({
  selector: 'app-authenticated-shell',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  styleUrl: './authenticated-shell.css',
  templateUrl: './authenticated-shell.html',
})
export class AuthenticatedShell {
  readonly auth = inject(AuthService);
  readonly signingOut = signal(false);
  readonly signOutError = signal('');
  private readonly router = inject(Router);

  logout(): void {
    if (this.signingOut()) return;
    this.signOutError.set('');
    this.signingOut.set(true);
    this.auth.logout().subscribe({
      next: () => {
        this.signingOut.set(false);
        void this.router.navigate(['/entrar']);
      },
      error: () => {
        this.signingOut.set(false);
        this.signOutError.set('Não foi possível confirmar a saída. Verifique a conexão e tente novamente.');
      },
    });
  }
}
