CREATE TABLE IF NOT EXISTS classes (
    id text PRIMARY KEY,
    name text NOT NULL,
    total_seats int NOT NULL CHECK (total_seats > 0)
);

CREATE TABLE IF NOT EXISTS holds (
    id text PRIMARY KEY,
    class_id text NOT NULL REFERENCES classes(id),
    student_id text NOT NULL,
    status text NOT NULL CHECK (status IN ('HELD', 'CONFIRMED', 'CANCELLED')),
    created_at timestamptz NOT NULL DEFAULT now(),
    expires_at timestamptz NOT NULL,
    confirmed_at timestamptz NULL
);

CREATE INDEX IF NOT EXISTS idx_holds_class_status ON holds (class_id, status);
CREATE INDEX IF NOT EXISTS idx_holds_student ON holds (student_id);
