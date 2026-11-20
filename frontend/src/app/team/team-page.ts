import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { CreateTeamMember, TeamApi, TeamMember, TeamRole, UpdateTeamMember } from './team-api';

interface TemporaryCredentialNotice {
  displayName: string;
  email: string;
  password: string;
}

@Component({
  selector: 'app-team-page',
  imports: [ReactiveFormsModule],
  templateUrl: './team-page.html',
  styleUrl: './team-page.css',
})
export class TeamPage implements OnInit {
  private readonly formBuilder = inject(FormBuilder);
  private readonly api = inject(TeamApi);
  readonly members = signal<TeamMember[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly page = signal(1);
  readonly pageSize = 20;
  readonly totalCount = signal(0);
  readonly error = signal('');
  readonly notice = signal('');
  readonly editorOpen = signal(false);
  readonly editingMember = signal<TeamMember | null>(null);
  readonly temporaryCredential = signal<TemporaryCredentialNotice | null>(null);
  readonly copied = signal(false);
  readonly roles: TeamRole[] = ['Administrador', 'Gestor', 'Operador'];
  readonly form = this.formBuilder.nonNullable.group({
    displayName: ['', [Validators.required, Validators.maxLength(200)]],
    email: ['', [Validators.required, Validators.email]],
    role: ['Operador' as TeamRole, Validators.required],
    temporaryPassword: ['', [Validators.required, Validators.minLength(6)]],
    isActive: [true],
  });

  ngOnInit(): void { this.load(); }

  load(): void {
    this.error.set('');
    this.loading.set(true);
    this.api.list(this.page(), this.pageSize).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: result => {
        this.members.set(result.items);
        this.page.set(result.page);
        this.totalCount.set(result.totalCount);
      },
      error: (error: HttpErrorResponse) => {
        this.error.set(error.status === 403
          ? 'Seu perfil não tem permissão para consultar a equipe. Apenas Administradores podem acessar esta área.'
          : error.status === 0
            ? 'Não foi possível conectar ao sistema. Verifique a conexão e tente novamente.'
            : 'Não foi possível carregar a equipe. Tente novamente.');
      },
    });
  }

  startCreate(): void {
    this.editingMember.set(null);
    this.form.reset({ displayName: '', email: '', role: 'Operador', temporaryPassword: '', isActive: true });
    this.form.controls.temporaryPassword.setValidators([Validators.required, Validators.minLength(6)]);
    this.form.controls.temporaryPassword.updateValueAndValidity();
    this.temporaryCredential.set(null);
    this.notice.set('');
    this.error.set('');
    this.editorOpen.set(true);
  }

  startEdit(member: TeamMember): void {
    this.editingMember.set(member);
    this.form.reset({
      displayName: member.displayName,
      email: member.email,
      role: member.role,
      temporaryPassword: '',
      isActive: member.isActive,
    });
    this.form.controls.temporaryPassword.clearValidators();
    this.form.controls.temporaryPassword.updateValueAndValidity();
    this.temporaryCredential.set(null);
    this.notice.set('');
    this.error.set('');
    this.editorOpen.set(true);
  }

  closeEditor(): void {
    if (this.saving()) return;
    this.editorOpen.set(false);
    this.editingMember.set(null);
    this.form.reset({ displayName: '', email: '', role: 'Operador', temporaryPassword: '', isActive: true });
  }

  save(): void {
    this.error.set('');
    this.notice.set('');
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    const values = this.form.getRawValue();
    const editing = this.editingMember();
    this.saving.set(true);
    const request = editing
      ? this.api.update(editing.id, {
        displayName: values.displayName.trim(),
        email: values.email.trim(),
        role: values.role,
        isActive: values.isActive,
      } satisfies UpdateTeamMember)
      : this.api.create({
        displayName: values.displayName.trim(),
        email: values.email.trim(),
        role: values.role,
        temporaryPassword: values.temporaryPassword,
      } satisfies CreateTeamMember);

    request.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: member => {
        if (!editing) {
          this.temporaryCredential.set({
            displayName: member.displayName,
            email: member.email,
            password: values.temporaryPassword,
          });
          this.copied.set(false);
          this.notice.set('Conta cadastrada. Entregue a senha temporária à pessoa fora do PIT Obras.');
        } else {
          this.notice.set('Dados da conta atualizados.');
          this.editorOpen.set(false);
          this.editingMember.set(null);
        }
        this.page.set(1);
        this.load();
      },
      error: (error: HttpErrorResponse) => {
        this.error.set(error.status === 409 && typeof error.error?.error === 'string'
          ? error.error.error
          : error.status === 400
            ? 'Confira os campos informados e os requisitos da senha temporária.'
            : error.status === 403
              ? 'Seu perfil não tem permissão para alterar a equipe.'
              : error.status === 0
                ? 'Não foi possível conectar ao sistema. Seus dados permanecem no formulário.'
                : 'Não foi possível salvar os dados. Tente novamente.');
      },
    });
  }

  async copyTemporaryPassword(): Promise<void> {
    const password = this.temporaryCredential()?.password;
    if (!password) return;
    try {
      await navigator.clipboard.writeText(password);
      this.copied.set(true);
    } catch {
      this.error.set('Não foi possível copiar automaticamente. Selecione e copie a senha exibida.');
    }
  }

  previousPage(): void {
    if (this.page() <= 1 || this.loading()) return;
    this.page.update(value => value - 1);
    this.load();
  }

  nextPage(): void {
    if (this.page() * this.pageSize >= this.totalCount() || this.loading()) return;
    this.page.update(value => value + 1);
    this.load();
  }

  rangeStart(): number { return this.totalCount() === 0 ? 0 : (this.page() - 1) * this.pageSize + 1; }
  rangeEnd(): number { return Math.min(this.page() * this.pageSize, this.totalCount()); }
}
