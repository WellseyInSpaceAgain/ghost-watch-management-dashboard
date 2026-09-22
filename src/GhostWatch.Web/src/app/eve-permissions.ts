// Permission comparisons are supplied by the API; the UI never maintains a scope list.
export interface EvePermissions {
  scopesKnown: boolean;
  hasAllRequiredScopes: boolean;
  missingScopeCount: number;
  missingScopes: string[];
}
