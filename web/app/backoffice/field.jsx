'use client';

// Every backoffice control sits inside one .bo-field wrapper so its label, control and hint are separate grid rows:
// the grid gap keeps the 3px + 3px focus ring clear of the label (S1).
export function Field({ id, label, optional, hint, check, children }) {
  const hintId = hint ? `${id}-hint` : undefined;
  const control = typeof children === 'function' ? children(hintId) : children;
  return <div className={check ? 'bo-field bo-check' : 'bo-field'}>
    {check && control}
    <label htmlFor={id}>{label}{optional && <span className="bo-optional">(opcional)</span>}</label>
    {!check && control}
    {hint && <small id={hintId} className="bo-hint">{hint}</small>}
  </div>;
}

// Renders one schema field (see schema.mjs). `options` resolves named option sets such as 'crops'.
export function SchemaControl({ field, id, value, onChange, optionSets = {} }) {
  const common = { id, required: !field.optional && field.type !== 'checkbox' };
  if (field.type === 'checkbox') {
    return <Field id={id} label={field.label} hint={field.hint} check>{hintId =>
      <input {...common} type="checkbox" checked={value} aria-describedby={hintId} onChange={event => onChange(event.target.checked)} />}</Field>;
  }
  return <Field id={id} label={field.label} optional={field.optional} hint={field.hint}>{hintId => {
    const props = { ...common, 'aria-describedby': hintId, value, onChange: event => onChange(event.target.value) };
    if (field.type === 'select') {
      const options = typeof field.options === 'string' ? optionSets[field.options] ?? [] : field.options;
      return <select {...props}>
        {(field.placeholder || field.optional) && <option value="">{field.placeholder ?? 'Sin indicar'}</option>}
        {options.map(option => <option key={option.value} value={option.value}>{option.label}</option>)}
      </select>;
    }
    if (field.type === 'textarea') return <textarea {...props} rows={3} maxLength={field.maxLength ?? 2000} />;
    const numeric = field.type === 'number' || field.type === 'integer';
    return <input {...props} type={numeric ? 'number' : field.type} inputMode={field.inputMode}
      min={field.min} max={field.max} step={field.step} maxLength={numeric ? undefined : field.maxLength ?? 200} />;
  }}</Field>;
}
