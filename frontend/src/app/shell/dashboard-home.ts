import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-dashboard-home',
  imports: [RouterLink],
  template: `
    <section class="overview-page" aria-labelledby="welcome-title">
      <header class="page-heading">
        <div>
          <p class="eyebrow">Visão geral</p>
          <h1 id="welcome-title">Sua operação, com clareza.</h1>
          <p class="welcome-copy">Organize os cadastros e registre os serviços que chegam à sua equipe.</p>
        </div>
        <a class="primary-link" routerLink="/demandas/nova"><span aria-hidden="true">＋</span> Nova demanda</a>
      </header>

      <section class="overview-section" aria-labelledby="quick-actions-title">
        <div class="section-heading">
          <div><p class="eyebrow">Ações rápidas</p><h2 id="quick-actions-title">Por onde começar</h2></div>
        </div>
        <div class="action-grid">
          <a class="action-card demand-action" routerLink="/demandas/nova">
            <span class="action-icon" aria-hidden="true">
              <svg viewBox="0 0 24 24"><path d="M12 5v14M5 12h14" /></svg>
            </span>
            <span class="action-copy"><small>Registrar solicitação</small><strong>Nova demanda</strong><span>Informe o cliente, local e detalhes do serviço.</span></span>
            <svg class="action-arrow" viewBox="0 0 24 24" aria-hidden="true"><path d="M5 12h14m-6-6 6 6-6 6" /></svg>
          </a>
          <a class="action-card customer-action" routerLink="/clientes">
            <span class="action-icon" aria-hidden="true">
              <svg viewBox="0 0 24 24"><circle cx="9" cy="8" r="3.2" /><path d="M3.5 20v-1.5a5.5 5.5 0 0 1 11 0V20zM16 5.2a3.2 3.2 0 0 1 0 6.2M17.5 14.5a4.9 4.9 0 0 1 3 4.5V20h-4" /></svg>
            </span>
            <span class="action-copy"><small>Cadastros da operação</small><strong>Clientes e locais</strong><span>Consulte clientes e mantenha seus locais de atendimento.</span></span>
            <svg class="action-arrow" viewBox="0 0 24 24" aria-hidden="true"><path d="M5 12h14m-6-6 6 6-6 6" /></svg>
          </a>
        </div>
      </section>

      <section class="intake-card" aria-labelledby="intake-title">
        <div class="intake-mark" aria-hidden="true">01</div>
        <div class="intake-copy">
          <p class="eyebrow">Abertura de serviço</p>
          <h2 id="intake-title">Quatro informações iniciam cada demanda</h2>
          <p>Tenha à mão o cliente, o local, a descrição do serviço e a origem do pedido.</p>
        </div>
        <div class="intake-tags" aria-label="Campos obrigatórios">
          <span>Cliente</span><span>Local</span><span>Descrição</span><span>Origem</span>
        </div>
      </section>
    </section>
  `,
  styles: [`
    :host { display: block; }
    .overview-page { max-width: 92rem; margin: 0 auto; }
    .page-heading { display: flex; align-items: flex-end; justify-content: space-between; gap: 1rem; margin-bottom: 2rem; }
    .eyebrow { margin: 0 0 .45rem; color: #8a6416; font-size: .69rem; font-weight: 800; letter-spacing: .12em; text-transform: uppercase; }
    h1 { margin: 0; color: var(--brand-ink); font-size: clamp(1.9rem, 3.8vw, 2.65rem); letter-spacing: -.055em; line-height: 1.12; }
    .welcome-copy { margin: .55rem 0 0; color: var(--brand-muted); font-size: .9rem; line-height: 1.55; }
    .primary-link { min-height: 2.75rem; display: inline-flex; align-items: center; justify-content: center; gap: .45rem; border-radius: .68rem; padding: .65rem .9rem; color: #27200e; background: var(--brand-primary); font-size: .8rem; font-weight: 760; text-decoration: none; white-space: nowrap; }
    .primary-link:hover { background: var(--brand-primary-hover); }
    .primary-link:focus-visible, .action-card:focus-visible { outline: 3px solid #edcf81; outline-offset: 3px; }
    .primary-link span { font-size: 1.1rem; line-height: 1; }
    .overview-section { margin-top: 1.8rem; }
    .section-heading { display: flex; align-items: center; justify-content: space-between; margin-bottom: .85rem; }
    .section-heading .eyebrow { margin-bottom: .2rem; }
    .section-heading h2, .intake-copy h2 { margin: 0; color: var(--brand-ink); font-size: 1.12rem; letter-spacing: -.025em; }
    .action-grid { display: grid; grid-template-columns: repeat(2,minmax(0,1fr)); gap: 1rem; }
    .action-card { min-height: 9rem; display: flex; align-items: center; gap: 1rem; border: 1px solid var(--brand-border); border-radius: var(--card-radius); padding: 1.2rem; color: inherit; background: var(--brand-surface); box-shadow: var(--card-shadow); text-decoration: none; transition: border-color .15s, transform .15s, box-shadow .15s; }
    .action-card:hover { transform: translateY(-2px); border-color: #e4c46f; box-shadow: 0 12px 26px rgb(28 29 31 / 7%); }
    .action-icon { width: 2.8rem; height: 2.8rem; flex: 0 0 auto; display: grid; place-items: center; border-radius: .82rem; color: #55400f; background: var(--brand-primary-soft); }
    .customer-action .action-icon { color: #4f4e47; background: #f0f0ec; }
    .action-icon svg, .action-arrow { width: 1.25rem; height: 1.25rem; fill: none; stroke: currentColor; stroke-linecap: round; stroke-linejoin: round; stroke-width: 1.7; }
    .action-copy { min-width: 0; display: grid; gap: .15rem; }
    .action-copy small { color: var(--brand-muted); font-size: .7rem; }
    .action-copy strong { color: var(--brand-ink); font-size: .98rem; }
    .action-copy > span { margin-top: .2rem; color: var(--brand-muted); font-size: .78rem; line-height: 1.5; }
    .action-arrow { flex: 0 0 auto; margin-left: auto; color: #86847d; }
    .intake-card { display: grid; grid-template-columns: auto minmax(14rem, 1fr) minmax(16rem, .9fr); align-items: center; gap: 1.15rem; margin-top: 1rem; border: 1px solid #eadcb7; border-radius: var(--card-radius); padding: 1.25rem; background: linear-gradient(105deg, #fffaf0, #fff 76%); }
    .intake-mark { width: 2.65rem; height: 2.65rem; display: grid; place-items: center; border-radius: .78rem; color: #58420f; background: #f2dfac; font-size: .76rem; font-weight: 820; }
    .intake-copy .eyebrow { margin-bottom: .25rem; }
    .intake-copy h2 { font-size: .98rem; }
    .intake-copy p:last-child { margin: .3rem 0 0; color: var(--brand-muted); font-size: .77rem; line-height: 1.45; }
    .intake-tags { display: flex; flex-wrap: wrap; justify-content: flex-end; gap: .4rem; }
    .intake-tags span { border: 1px solid #ebdfbf; border-radius: 999px; padding: .3rem .55rem; color: #6b5727; background: rgb(255 255 255 / 75%); font-size: .68rem; font-weight: 650; }
    @media (max-width: 760px) { .page-heading { align-items: flex-start; flex-direction: column; } .action-grid { grid-template-columns: 1fr; } .intake-card { grid-template-columns: auto 1fr; } .intake-tags { grid-column: 1 / -1; justify-content: flex-start; } }
    @media (max-width: 420px) { .action-card { gap: .75rem; padding: .9rem; } .action-icon { width: 2.4rem; height: 2.4rem; } .action-copy > span { font-size: .74rem; } .intake-card { padding: .95rem; } }
  `],
})
export class DashboardHome {}
