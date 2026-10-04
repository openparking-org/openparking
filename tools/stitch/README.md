# Stitch connection

This local MCP proxy uses Google's Stitch SDK and reads `STITCH_API_KEY` from
the repository root `.env`. The key stays out of MCP configuration and the web
application. Restart Codex to load the registered `stitch` server into its tool
catalog.

Install dependencies with `npm.cmd ci` in this directory. Test from the repository
root with `node tools/stitch/connect.mjs --check`. The default invocation starts
the STDIO MCP proxy.

The optional `--call TOOL INPUT_JSON OUTPUT_JSON` mode calls the same Stitch MCP
server and saves its result for local visual design work. Generated HTML is a
reference artifact; the application uses its existing React implementation.
