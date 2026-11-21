// Invented names and events exclusively for the local visual prototype.
export const plots = [
  { id: '03', client: 'Cooperativa Horizonte (demo)', field: 'La Cañada (demo)', name: 'Lote 03', crop: 'Trigo ficticio', area: '24 ha ficticias', status: 'En observación', history: ['12 sep · Recorrido simulado: registro visual de cobertura.', '04 sep · Nota de ejemplo: planificación de visita.'], order: 'Orden ficticia #DEMO-014 · Recorrido visual · Sin ejecución', demo: true },
  { id: '07', client: 'Cooperativa Horizonte (demo)', field: 'La Cañada (demo)', name: 'Lote 07', crop: 'Avena ficticia', area: '18 ha ficticias', status: 'Pendiente', history: ['10 sep · Nota simulada: revisión de acceso.', '02 sep · Registro de ejemplo: apertura de ficha.'], order: 'Orden ficticia #DEMO-015 · Visita de muestra · Sin ejecución', demo: true },
  { id: '12', client: 'Estancia Bruma (demo)', field: 'El Ombú (demo)', name: 'Lote 12', crop: 'Maíz ficticio', area: '31 ha ficticias', status: 'En observación', history: ['08 sep · Recorrido simulado: observación general.', '01 sep · Nota de ejemplo: ficha creada.'], order: 'Orden ficticia #DEMO-016 · Relevamiento visual · Sin ejecución', demo: true },
  { id: '02', client: 'Granja del Viento (demo)', field: 'Las Lomas (demo)', name: 'Lote 02', crop: 'Girasol ficticio', area: '15 ha ficticias', status: 'Pendiente', history: ['06 sep · Nota simulada: próxima visita.', '28 ago · Registro de ejemplo: ficha creada.'], order: 'Orden ficticia #DEMO-017 · Visita de muestra · Sin ejecución', demo: true },
];

export function searchPlots(query) {
  const words = query.trim().toLocaleLowerCase('es').split(/\s+/).filter(Boolean);
  return plots.filter(plot => words.every(word => [plot.client, plot.field, plot.name].join(' ').toLocaleLowerCase('es').includes(word)));
}
