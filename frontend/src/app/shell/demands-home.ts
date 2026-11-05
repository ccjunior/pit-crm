import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-demands-home',
  imports: [RouterLink],
  template: `
    <section class="welcome-card" aria-labelledby="welcome-title">
      <p class="eyebrow">Área de trabalho</p>
      <h1 id="welcome-title">Demandas</h1>
      <p class="welcome-copy">Abra uma demanda e acompanhe a operação a partir do pedido recebido.</p>
      <a class="new-demand-link" routerLink="/demandas/nova">＋ Registrar nova demanda</a>
      <div class="empty-state" role="status">
        <span class="empty-icon" aria-hidden="true">◈</span>
        <div>
          <h2>Pronto para começar</h2>
          <p>A consulta e o acompanhamento das demandas serão adicionados nas próximas tarefas.</p>
        </div>
      </div>
    </section>
  `,
  styles: [`
    .welcome-card { max-width: 62rem; }
    .eyebrow { margin: 0 0 .55rem; color: #147d73; font-size: .72rem; font-weight: 800; letter-spacing: .12em; text-transform: uppercase; }
    h1 { margin: 0; color: #142c49; font-size: clamp(2rem, 4vw, 2.8rem); letter-spacing: -.05em; }
    .welcome-copy { margin: .75rem 0 2rem; color: #63748a; line-height: 1.6; }
    .new-demand-link { display:inline-flex; margin:0 0 1.4rem; border-radius:.45rem; padding:.65rem .85rem; color:white; background:#147d73; font-size:.84rem; font-weight:750; text-decoration:none; }
    .new-demand-link:focus-visible { outline:3px solid #85cfc3; outline-offset:2px; }
    .empty-state { display: flex; align-items: flex-start; gap: 1rem; border: 1px solid #e0e8ef; border-radius: .75rem; padding: 1.35rem; background: #fff; box-shadow: 0 .55rem 1.8rem rgb(20 44 73 / 4%); }
    .empty-icon { width: 2.4rem; height: 2.4rem; flex: 0 0 auto; display: grid; place-items: center; border-radius: .65rem; color: #147d73; background: #e8f5f2; font-size: 1.2rem; }
    h2 { margin: .1rem 0 .4rem; color: #203954; font-size: 1rem; }
    .empty-state p { margin: 0; color: #6c7c90; font-size: .88rem; line-height: 1.5; }
  `],
})
export class DemandsHome {}
