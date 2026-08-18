import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useState, type ReactNode } from "react";
import { HubProvider } from "@/lib/bus";
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
      <QueryClientProvider client={client}>
        <HubProvider>{children}</HubProvider>
      </QueryClientProvider>
    </ErrorBoundary>
  );
}
