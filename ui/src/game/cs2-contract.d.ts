// Narrow, authored compile-time contract based on the public template declarations.
// Replace with the INSTALLED template's types before shipping. This does not prove API compatibility.
declare module "cs2/api" {
    export interface ValueBinding<T> {
        readonly value: T;
    }
    export function bindValue<T>(
        group: string,
        name: string,
        fallback: T,
    ): ValueBinding<T>;
    export function useValue<T>(binding: ValueBinding<T>): T;
    export function trigger(
        group: string,
        name: string,
        ...args: unknown[]
    ): void;
}
declare module "cs2/ui" {
    import type { ComponentType, PropsWithChildren, ReactNode } from "react";
    export const Panel: ComponentType<
        PropsWithChildren<{ header?: ReactNode; className?: string }>
    >;
    export const Button: ComponentType<
        PropsWithChildren<{
            onSelect?: () => void;
            disabled?: boolean;
            selected?: boolean;
            className?: string;
            tooltipLabel?: ReactNode;
        }>
    >;
    export const Icon: ComponentType<{ src: string; className?: string }>;
    export const Dropdown: ComponentType<
        PropsWithChildren<{ content: ReactNode }>
    >;
    export const DropdownToggle: ComponentType<PropsWithChildren>;
    export const DropdownItem: ComponentType<
        PropsWithChildren<{
            value: string;
            selected?: boolean;
            onChange?: (value: string) => void;
        }>
    >;
}
declare module "cs2/modding" {
    import type { ComponentType } from "react";
    export type ModRegistrar = (registry: {
        append: (
            anchor: "Game" | "UniversalModMenu",
            component: ComponentType,
        ) => void;
    }) => void;
}
