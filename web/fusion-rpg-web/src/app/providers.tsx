import { I18nProvider } from "@lingui/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useState, type ReactNode } from "react";
import { HubProvider } from "@/lib/bus";
import { i18n } from "@/i18n";
import { ErrorBoundary } from "./ErrorBoundary";

export function AppProviders({ children }: { children: ReactNode }) {
  const [client] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            staleTime: 5000,
            retry: 1,
            refetchOnWindowFocus: false
          }
        }
      })
  );

  return (
    <ErrorBoundary>
      <I18nProvider i18n={i18n}>
        <QueryClientProvider client={client}>
          <HubProvider>{children}</HubProvider>
        </QueryClientProvider>
      </I18nProvider>
    </ErrorBoundary>
  );
}
