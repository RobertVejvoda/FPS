// LOC001 (#744) — catalog typing helper. A translated catalog must cover
// every English key (missing translations fail typecheck) and may add extra
// plural-variant keys (e.g. Czech `.few`) that English doesn't need.
export type Catalog<K extends string> = Record<K, string> & Record<string, string>;
