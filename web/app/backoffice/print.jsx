'use client';

import { useEffect, useState } from 'react';
import { api, sessionEndedMessage } from './api.mjs';
import { cropLabel, displayDate, documentTitle, productLabel } from './work-orders.mjs';

const plotLabel = plot => plot.plotCode ? <><strong>{plot.plotCode}</strong> <span className="ot-sub">{plot.plotName}</span></> : plot.plotName;

function Box({ title, children, className }) {
  return <section className={className ? `ot-box ${className}` : 'ot-box'}><h2>{title}</h2>{children}</section>;
}

// The issued OT as a printable sheet (S5), in the sections of the engineers' PDFs. "Descargar PDF" opens the browser's
// print dialog, where "Guardar como PDF" proposes document.title, the workbook-pattern file name.
export default function WorkOrderPrint({ id }) {
  const [order, setOrder] = useState(null);
  const [problem, setProblem] = useState(null);

  useEffect(() => {
    let active = true;
    api(`work-orders/${encodeURIComponent(id)}/document`).then(loaded => { if (active) setOrder(loaded); })
      .catch(cause => { if (active) setProblem(cause.message === sessionEndedMessage ? 'session' : cause.message); });
    return () => { active = false; };
  }, [id]);
  useEffect(() => { if (order) document.title = documentTitle(order); }, [order]);

  const toolbar = <nav className="ot-toolbar" aria-label="Acciones de la orden">
    {order && <button type="button" onClick={() => window.print()}>Descargar PDF</button>}
    <a href="/backoffice">Volver</a>
    {order && <small>En el diálogo de impresión elige “Guardar como PDF”; el nombre del archivo ya viene propuesto.</small>}
  </nav>;

  if (!order) return <main className="ot-page">{toolbar}<div className="ot-message">
    {problem === 'session' ? <p role="alert">Tu sesión terminó o aún no ingresaste. <a href="/backoffice">Ingresa al backoffice</a> y vuelve a abrir esta orden.</p>
      : problem ? <p role="alert">{problem}</p> : <p role="status">Cargando orden…</p>}</div></main>;

  const cancelled = order.status === 'cancelada';
  const labor = order.labor.abbreviation ? `${order.labor.name} (${order.labor.abbreviation})` : order.labor.name;
  const hollow = order.lines.some(line => line.hollowCone);
  return <main className="ot-page">{toolbar}
    <article className={cancelled ? 'ot-sheet ot-cancelled' : 'ot-sheet'} aria-label={`Orden de trabajo Nº ${order.number}`}>
      <header className="ot-head"><p className="ot-mark"><span aria-hidden="true">✳</span> Agro · Operaciones</p><h1>ORDEN DE TRABAJO</h1></header>
      {cancelled && <p className="ot-void" role="note"><strong>ANULADA</strong>{order.cancelReason && <span>Motivo: {order.cancelReason}</span>}</p>}

      <div className="ot-row ot-row-head">
        <Box title="Firma comercial"><p className="ot-strong">{order.firm.name}</p>
          {order.firm.taxId && <p>CUIT {order.firm.taxId}</p>}
          {order.clientName !== order.firm.name && <p>Cliente {order.clientName}</p>}</Box>
        <Box title="Orden Nº" className="ot-number"><p className="ot-big">{order.number}</p>
          <h2>Fecha</h2><p className="ot-strong">{displayDate(order.issuedOn)}</p></Box>
      </div>

      <div className="ot-row">
        <Box title="Labor a realizar"><p className="ot-strong">{labor}</p></Box>
        <Box title="Etapa"><p>{order.stageName ?? 'Sin indicar'}</p></Box>
      </div>

      <table className="ot-table">
        <thead><tr><th scope="col">Campo</th><th scope="col">Lote</th><th scope="col" className="ot-num">Has</th><th scope="col" className="ot-num">Has tarea</th></tr></thead>
        <tbody>{order.plots.map((plot, index) => <tr key={index}>
          <td>{plot.fieldName}</td><td>{plotLabel(plot)}</td>
          <td className="ot-num">{plot.areaHectares}</td><td className="ot-num">{plot.workedAreaHectares}</td></tr>)}</tbody>
        <tfoot><tr><th scope="row" colSpan={3}>Total a trabajar</th><td className="ot-num">{order.totalWorkedAreaHectares} ha</td></tr></tfoot>
      </table>

      <div className="ot-row">
        <Box title="Contratista"><p className="ot-strong">{order.contractor.name}</p>{order.contractor.ownEquipment && <p>Equipo propio</p>}</Box>
        <Box title="Cultivo / Rotación"><p className="ot-strong">{cropLabel(order)}</p>{order.campaignName && <p>Campaña {order.campaignName}</p>}</Box>
      </div>

      <section className="ot-section"><h2>Insumos a utilizar</h2>
        {!order.lines.length ? <p>Esta orden no lleva insumos.</p> : <>
          <table className="ot-table">
            <thead><tr><th scope="col">Producto</th><th scope="col" className="ot-num">Dosis por ha</th><th scope="col" className="ot-num">Total</th></tr></thead>
            <tbody>{order.lines.map(line => <tr key={line.position}>
              <td>{productLabel(line)}{line.hollowCone && <span className="ot-tag">Cono hueco</span>}</td>
              <td className="ot-num">{line.dosePerHectare} {line.unit}/ha</td><td className="ot-num">{line.total} {line.unit}</td></tr>)}</tbody>
          </table>
          {hollow && <p className="ot-note">Los productos marcados “Cono hueco” requieren aplicación con cono hueco.</p>}</>}
      </section>

      <section className="ot-section"><h2>Observaciones</h2><p className="ot-notes">{order.observations ?? 'Sin observaciones.'}</p></section>

      <footer className="ot-foot">
        <div><p className="ot-warning">NO MODIFICAR A MANO ESTA ORDEN.</p>
          <p>Ante cualquier duda llamar: <strong>{order.responsibleEngineer ?? 'Sin indicar'}</strong></p></div>
        <div className="ot-sign"><span>Firma responsable actuante</span></div>
      </footer>
    </article>
  </main>;
}
