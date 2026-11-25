import { Component, inject, signal } from '@angular/core';
import { finalize } from 'rxjs';
import { ProposalApi, MyActionItem, proposalStages } from './proposal-api';
import { formatDateOnly, formatScheduledAt } from './proposal-time';

@Component({
  selector: 'app-my-actions-page',
  standalone: true,
  templateUrl: './my-actions-page.html',
  styleUrl: './my-actions-page.css',
})
export class MyActionsPage {
  private readonly api = inject(ProposalApi);
  readonly items = signal<MyActionItem[]>([]);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly page = signal(1);
  readonly pageSize = 12;
  readonly totalCount = signal(0);
  private requestId = 0;

  constructor() { this.load(); }

  load(page = this.page()): void {
    this.page.set(page);
    const requestId = ++this.requestId;
    this.loading.set(true); this.error.set('');
    this.api.myActions(page, this.pageSize).pipe(finalize(() => {
      if (requestId === this.requestId) this.loading.set(false);
    })).subscribe({
      next: result => {
        if (requestId !== this.requestId) return;
        this.items.set(result.items); this.totalCount.set(result.totalCount);
      },
      error: error => {
        if (requestId === this.requestId) {
          this.error.set(error.status === 403
            ? 'Seu perfil não tem acesso às próximas ações.'
            : 'Não foi possível carregar suas próximas ações. Tente novamente.');
        }
      },
    });
  }

  previousPage(): void { if (this.page() > 1 && !this.loading()) this.load(this.page() - 1); }
  nextPage(): void { if (this.page() * this.pageSize < this.totalCount() && !this.loading()) this.load(this.page() + 1); }
  rangeStart(): number { return this.totalCount() ? (this.page() - 1) * this.pageSize + 1 : 0; }
  rangeEnd(): number { return Math.min(this.page() * this.pageSize, this.totalCount()); }
  formatDateOnly(value: string | null): string { return formatDateOnly(value); }
  formatScheduledAt(value: string): string { return formatScheduledAt(value); }
  statusLabel(status: string): string { return proposalStages.find(stage => stage.id === status)?.label ?? status; }
}
