// Type declarations for workspace development before npm install
declare module 'react' {
  export = React;
}

declare namespace React {
  export type ReactNode = any;
  export type FC<P = {}> = (props: P) => any;
  export function useState<T>(initial: T | (() => T)): [T, (val: T | ((prev: T) => T)) => void];
  export function useEffect(effect: () => void | (() => void), deps?: any[]): void;
  export function useCallback<T extends (...args: any[]) => any>(fn: T, deps: any[]): T;
  export namespace JSX {
    interface IntrinsicElements {
      [elemName: string]: any;
    }
  }
  export interface MouseEvent<T = Element> {
    currentTarget: T;
    clientX: number;
    clientY: number;
  }
  export interface ChangeEvent<T = Element> {
    target: T;
  }
}

declare module 'react-dom/client' {
  export function createRoot(container: Element | DocumentFragment): {
    render(children: any): void;
    unmount(): void;
  };
}

declare module 'react/jsx-runtime' {
  export const jsx: any;
  export const jsxs: any;
  export const Fragment: any;
}

declare module 'lucide-react' {
  export const MapPin: any;
  export const Navigation: any;
  export const Save: any;
  export const Plus: any;
  export const ShieldAlert: any;
  export const CheckCircle: any;
  export const XCircle: any;
  export const TrendingUp: any;
  export const Users: any;
  export const DollarSign: any;
  export const Clock: any;
  export const Sliders: any;
  export const RefreshCw: any;
  export const Calendar: any;
  export const CheckCircle2: any;
  export const Car: any;
  export const LayoutDashboard: any;
  export const Map: any;
  export const CalendarCheck: any;
  export const ShieldCheck: any;
}

declare module 'zustand' {
  export function create<T>(stateCreator: any): () => T;
}

declare module '@microsoft/signalr' {
  export class HubConnection {
    state: any;
    start(): Promise<void>;
    stop(): Promise<void>;
    invoke(methodName: string, ...args: any[]): Promise<any>;
    on(methodName: string, newMethod: (...args: any[]) => void): void;
  }
  export class HubConnectionBuilder {
    withUrl(url: string): this;
    withAutomaticReconnect(): this;
    build(): HubConnection;
  }
  export enum HubConnectionState {
    Connected = 'Connected',
    Disconnected = 'Disconnected',
    Connecting = 'Connecting',
    Reconnecting = 'Reconnecting'
  }
}

declare module 'vitest' {
  export function describe(name: string, fn: () => void): void;
  export function it(name: string, fn: () => void | Promise<void>): void;
  export function expect(actual: any): any;
}

declare module 'vite' {
  export function defineConfig(config: any): any;
}

declare module '@vitejs/plugin-react' {
  export default function react(): any;
}
