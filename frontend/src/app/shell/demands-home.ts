import { Component } from '@angular/core';

@Component({
  selector: 'app-demands-home',
  template: `
    <section class="welcome-card" aria-labelledby="welcome-title">
      <p class="eyebrow">Área de trabalho</p>
      <h1 id="welcome-title">Demandas</h1>
      <p class="welcome-copy">Sua sessão está ativa. A lista de demandas será conectada nesta etapa da entrega.</p>
      <div class="empty-state" role="status">
        <span class="empty-icon" aria-hidden="true">◈</span>
        <div>
          <h2>Pronto para começar</h2>
          <p>Quando os cadastros forem liberados, suas demandas aparecerão aqui.</p>
        </div>
      </div>
    </section>
  `,
  styles: [`
    .welcome-card { max-width: 62rem; }
    .eyebrow { margin: 0 0 .55rem; color: #147d73; font-size: .72rem; font-weight: 800; letter-spacing: .12em; text-transform: uppercase; }
    h1 { margin: 0; color: #142c49; font-size: clamp(2rem, 4vw, 2.8rem); letter-spacing: -.05em; }
    .welcome-copy { margin: .75rem 0 2rem; color: #63748a; line-height: 1.6; }
    .empty-state { display: flex; align-items: flex-start; gap: 1rem; border: 1px solid #e0e8ef; border-radius: .75rem; padding: 1.35rem; background: #fff; box-shadow: 0 .55rem 1.8rem rgb(20 44 73 / 4%); }
    .empty-icon { width: 2.4rem; height: 2.4rem; flex: 0 0 auto; display: grid; place-items: center; border-radius: .65rem; color: #147d73; background: #e8f5f2; font-size: 1.2rem; }
    h2 { margin: .1rem 0 .4rem; color: #203954; font-size: 1rem; }
    .empty-state p { margin: 0; color: #6c7c90; font-size: .88rem; line-height: 1.5; }
  `],
})
export class DemandsHome {}
