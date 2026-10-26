import React, {
    createContext,
    useContext,
    useEffect,
    useId,
    useState,
    type ComponentType,
    type PropsWithChildren,
    useRef,
} from "react";
export interface ButtonProps extends PropsWithChildren {
    onSelect?: () => void;
    disabled?: boolean;
    selected?: boolean;
    title?: string;
    className?: string;
    "aria-expanded"?: boolean;
    "aria-controls"?: string;
}
export interface PanelProps extends PropsWithChildren {
    header: React.ReactNode;
    className?: string;
}
export interface ChoiceProps {
    label: string;
    value: string;
    options: { value: string; label: string }[];
    onChange: (value: string) => void;
}
export interface Primitives {
    Button: ComponentType<ButtonProps>;
    Panel: ComponentType<PanelProps>;
    Choice: ComponentType<ChoiceProps>;
}
export const Controls = createContext<Primitives | null>(null);
export function useControls() {
    const controls = useContext(Controls);
    if (!controls) throw new Error("UI primitives missing");
    return controls;
}
export function Foldout({
    title,
    children,
    initial = false,
}: PropsWithChildren<{ title: string; initial?: boolean }>) {
    const [open, setOpen] = useState(initial);
    const { Button } = useControls();
    const id = useId();
    return (
        <section className="td-foldout">
            <Button
                aria-expanded={open}
                aria-controls={id}
                onSelect={() => setOpen(!open)}
                title={open ? "Collapse settings" : "Expand settings"}
            >
                <span aria-hidden>{open ? "▾" : "▸"} </span>
                {title}
            </Button>
            {open && (
                <div id={id} className="td-foldout-body">
                    {children}
                </div>
            )}
        </section>
    );
}
export function NumberField({
    label,
    value,
    onChange,
    min,
    max,
    step = 1,
}: {
    label: string;
    value: number;
    onChange: (n: number) => void;
    min?: number;
    max?: number;
    step?: number;
}) {
    const [text, setText] = useState(String(value));
    const [error, setError] = useState("");
    const id = useId();
    useEffect(() => {
        setText(String(value));
        setError("");
    }, [value]);
    return (
        <label className="td-field" htmlFor={id}>
            <span>{label}</span>
            <input
                id={id}
                type="number"
                value={text}
                min={min}
                max={max}
                step={step}
                aria-invalid={!!error}
                aria-describedby={error ? `${id}-error` : undefined}
                onChange={(e) => {
                    const s = e.target.value;
                    setText(s);
                    const n = Number(s);
                    if (
                        s.trim() === "" ||
                        !Number.isFinite(n) ||
                        (min !== undefined && n < min) ||
                        (max !== undefined && n > max)
                    ) {
                        setError(
                            `Enter a number${min !== undefined ? ` ≥ ${min}` : ""}${max !== undefined ? ` and ≤ ${max}` : ""}.`,
                        );
                        return;
                    }
                    try {
                        onChange(n);
                        setError("");
                    } catch (err) {
                        setError(
                            err instanceof Error
                                ? err.message
                                : "Invalid value",
                        );
                    }
                }}
            />
            {error && (
                <small id={`${id}-error`} role="alert">
                    {error}
                </small>
            )}
        </label>
    );
}

export function Modal({
    children,
    title,
    onClose,
}: PropsWithChildren<{ title: string; onClose: () => void }>) {
    const ref = useRef<HTMLDivElement>(null);
    useEffect(() => {
        const previous = document.activeElement as HTMLElement | null;
        const first = ref.current?.querySelector<HTMLElement>(
            'input,button,[tabindex="0"]',
        );
        first?.focus();
        return () => previous?.focus();
    }, []);
    return (
        <div
            className="td-modal-backdrop"
            onClick={(e) => e.stopPropagation()}
            onPointerDown={(e) => e.stopPropagation()}
            onWheel={(e) => e.stopPropagation()}
        >
            <div
                ref={ref}
                className="td-dialog"
                role="dialog"
                aria-modal="true"
                aria-label={title}
                onKeyDown={(e) => {
                    e.stopPropagation();
                    if (e.key === "Escape") {
                        e.preventDefault();
                        onClose();
                    }
                    if (e.key === "Tab") {
                        const elements = Array.from(
                            ref.current?.querySelectorAll<HTMLElement>(
                                'button:not([disabled]),input:not([disabled]),select:not([disabled]),[tabindex="0"]',
                            ) ?? [],
                        );
                        const first = elements[0],
                            last = elements[elements.length - 1];
                        if (e.shiftKey && document.activeElement === first) {
                            e.preventDefault();
                            last?.focus();
                        } else if (
                            !e.shiftKey &&
                            document.activeElement === last
                        ) {
                            e.preventDefault();
                            first?.focus();
                        }
                    }
                }}
            >
                {children}
            </div>
        </div>
    );
}
