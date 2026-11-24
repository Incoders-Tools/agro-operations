-- Additive work orders (OT), as the workbook `Orden` sheet. Runs after 001 and 003-007 in one transaction.
-- Every statement is safe to rerun and keeps existing rows.

-- OT numbers are consecutive per commercial firm. The create transaction takes the firm's next number with
-- UPDATE ... RETURNING, so concurrent creates serialize on the firm row and a rolled-back create leaves no gap.
-- The number is editable so the numbering can continue from the workbook.
ALTER TABLE commercial_firms
    ADD COLUMN IF NOT EXISTS next_work_order_number integer NOT NULL DEFAULT 1 CHECK (next_work_order_number > 0);

-- A cancelled OT keeps its number and stays queryable; only a cancelled OT has a reason.
CREATE TABLE IF NOT EXISTS work_orders (
    id uuid PRIMARY KEY,
    firm_id uuid NOT NULL REFERENCES commercial_firms(id) ON DELETE RESTRICT,
    number integer NOT NULL CHECK (number > 0),
    issued_on date NOT NULL,
    labor_type_id uuid NOT NULL REFERENCES labor_types(id) ON DELETE RESTRICT,
    stage_id uuid REFERENCES stages(id) ON DELETE RESTRICT,
    contractor_id uuid NOT NULL REFERENCES contractors(id) ON DELETE RESTRICT,
    field_id uuid NOT NULL REFERENCES fields(id) ON DELETE RESTRICT,
    campaign_id uuid REFERENCES campaigns(id) ON DELETE RESTRICT,
    crop_id uuid REFERENCES crops(id) ON DELETE RESTRICT,
    variety text CHECK (length(btrim(variety)) > 0),
    observations text CHECK (length(btrim(observations)) > 0),
    responsible_engineer text CHECK (length(btrim(responsible_engineer)) > 0),
    status text NOT NULL DEFAULT 'emitida' CHECK (status IN ('emitida', 'realizada', 'cancelada')),
    cancel_reason text CHECK (length(btrim(cancel_reason)) > 0),
    created_at timestamptz NOT NULL DEFAULT now(),
    revision bigint NOT NULL DEFAULT 1 CHECK (revision > 0),
    UNIQUE (firm_id, number),
    CHECK ((status = 'cancelada') = (cancel_reason IS NOT NULL))
);
CREATE INDEX IF NOT EXISTS work_orders_field_id_idx ON work_orders (field_id);
CREATE INDEX IF NOT EXISTS work_orders_status_idx ON work_orders (status);

-- area_hectares is the plot area when the OT was written; worked area may exceed it (the API warns).
CREATE TABLE IF NOT EXISTS work_order_plots (
    work_order_id uuid NOT NULL REFERENCES work_orders(id) ON DELETE RESTRICT,
    plot_id uuid NOT NULL REFERENCES plots(id) ON DELETE RESTRICT,
    area_hectares numeric(12,4) NOT NULL CHECK (area_hectares > 0),
    worked_area_hectares numeric(12,4) NOT NULL CHECK (worked_area_hectares > 0),
    PRIMARY KEY (work_order_id, plot_id)
);
CREATE INDEX IF NOT EXISTS work_order_plots_plot_id_idx ON work_order_plots (plot_id);

-- unit is the input unit when the line was written; total = dose per hectare x the OT's worked area,
-- computed by the API and rounded to 4 decimals. A dose outside the input range is warned, never changed.
CREATE TABLE IF NOT EXISTS work_order_lines (
    id uuid PRIMARY KEY,
    work_order_id uuid NOT NULL REFERENCES work_orders(id) ON DELETE RESTRICT,
    position smallint NOT NULL CHECK (position > 0),
    input_id uuid NOT NULL REFERENCES inputs(id) ON DELETE RESTRICT,
    dose_per_hectare numeric(12,4) NOT NULL CHECK (dose_per_hectare > 0),
    unit text NOT NULL CHECK (length(btrim(unit)) > 0),
    total numeric(14,4) NOT NULL CHECK (total > 0),
    UNIQUE (work_order_id, position)
);
CREATE INDEX IF NOT EXISTS work_order_lines_input_id_idx ON work_order_lines (input_id);
