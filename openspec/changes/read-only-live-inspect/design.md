# Design

Reference the application from the isolated RealApiTests project (still excluded from solution execution). Reuse its existing pinned authentication and sanitized HTTP boundary. Supply an IGameHttpClient adapter that rejects POST and allows only exact inspection GET routes. Invoke StrategySessionFactory and InspectAsync directly; do not instantiate the web host. Use a new RealApiInspect category and the existing explicit read-only environment guard.

Offline tests prove forbidden routes never reach the underlying handler and allowed paginated reads retain the bearer boundary. Live evidence distinguishes a selected command from a blocked decision; neither is evidence of a dispatched action. Docker daemon absence remains a separate operational blocker.
