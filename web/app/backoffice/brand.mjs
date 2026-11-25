// Product brand (S4): the one place that names the product, next to brand.css, which holds its colours and fonts.

export const brandName = 'Terra IA';
export const brandTagline = 'Operaciones agronómicas';

/** Document title: the brand and what the page is. */
export const brandTitle = (page = 'Operaciones') => `${brandName} · ${page}`;
