# Contributing

Keep the command-driven adventure readable, fair and consistent. New content should reward observation and experimentation rather than guessing an exact phrase.

Before proposing a change, read the [development guide](docs/Development.md). Include the problem, expected behavior and relevant checks in a pull request. For a puzzle change, describe how a player finds the necessary clues and how the solution is verified. Mark spoilers clearly.

Use fictional explorers and `example.test` addresses in fixtures. Never commit saves, databases, session tokens, mail captures, credentials or screenshots of real player accounts.

Run the .NET checks and Python tests. Build the frontend when changing the interface, and inspect it at desktop and mobile sizes. Keep the guides and wiki sources aligned when commands, routes or content formats change.

For security issues, follow [SECURITY.md](SECURITY.md) rather than posting exploit details publicly.
