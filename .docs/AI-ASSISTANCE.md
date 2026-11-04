# AI word assistance

Implemented 2026-11-04 for optional checks and unfamiliar-word identification.
This is separate from the dictionary, morphology pipeline and Explain grammar.
The [user guide](USER-GUIDE.md) covers setup and buttons.

## Providers and connection

ChatGPT is the initial provider; Off and Ollama are selectable, with model choices
saved per provider. Model catalogs are fetched on request and after connecting or
switching accounts/providers. No hardcoded ChatGPT model or Ollama model is
required. Ollama discovery uses its running service, not a fixed manifests path,
so models installed/removed through Ollama appear on refresh.

ChatGPT uses the documented dynamic public-client flow for eligible local apps:
PKCE S256, fresh state and nonce, a stable per-installation UUID host identifier,
and an IPv4 loopback callback started before opening the system browser. The
issued client ID is retained (including incomplete registrations), reused on
reauthorization, and bound to the verified account identity. Multiple registrations
remain separate even if they have the same email. IdentityModel validates RS256
signature with the official JWKS, issuer, audience, lifetime, nonce and authorized
party. Inference requires an explicit ChatGPT-plan scope grant.

Gengo uses only its own registrations. `ai/connections.bin` stores connection
metadata/tokens encrypted by Windows DPAPI CurrentUser; it is outside personal
SQLite and is not in backups. Writes are atomic. A file lock serializes credential
changes/rotating-token refreshes across app instances. No tokens or authorization
URLs are logged. Sign-out attempts refresh-token revocation, clears local tokens,
retains the client/account mapping, and reports unconfirmed remote revocation.

ChatGPT model discovery and inference use the public `/v1/models` and
`/v1/responses` endpoints with this connection's OAuth bearer token. Responses are
not stored server-side by the request (`store:false`); HTTP SSE is required and
only a terminal completed response can become an answer. Failures, including
usage errors after streaming begins, discard partial output. No API key, private
ChatGPT backend endpoint or Codex subprocess is used.

Ollama uses only `http://127.0.0.1:11434`, without a proxy or redirects. `/api/tags`
refreshes installed models, `/api/show` checks the chosen model for remote/cloud
configuration, and `/api/chat` requests structured JSON. Missing/removed models
are rejected before inference. No models are installed or downloaded. Provider
failure never triggers another provider automatically.

## Requests, presentation and limits

Check definition and Help identify expand the same inline Search panel, with
editable context and question. Each explicit Ask AI sends the selected expression,
all senses/restrictions of that candidate, available analyzer reading/inflection,
the shown context, and only that panel's earlier exchanges. Japanese is literal
Unicode in the inner prompt JSON, rather than `\\u` escapes. Passage and dictionary
text are untrusted quoted data. There are no tools, web searches or history reads.

Limits: expression/question 2,000 characters, context 12,000, at most six previous
exchanges, serialized payload 200,000 characters, structured answer 20,000 with
individual fields at most 2,000 and at most six alternatives. Network responses
are bounded to 1 MiB; inference times out after three minutes. Longer context is
windowed around the selected occurrence and is editable before sending. Responses
are validated structurally; that does not establish linguistic correctness.

The answer displays meaning, explanation, alternatives and uncertainty with the
provider/model label. It does not replace dictionary senses. A suggested meaning
can be copied, or expression/reading/meaning/category can prefill the existing
inline personal-entry form. Review and explicit Save entry are required for
persistence. AI actions themselves write no searches/clicks/personal entries.

Changing the selection, search, provider, model or account cancels the current
request. A revision check discards even responses from a provider that ignores
cancellation. Closing/canceling a panel also invalidates it. Answers/conversations
are transient and no persistent AI cache is used. Built-in dictionary/grammar
operation remains available when AI is Off, disconnected or unavailable.

## Actual evaluation and gaps

Synthetic transport tests cover model discovery, payloads, structured validation,
SSE terminal/error handling, cancellation, PKCE/callback checks, signed identity
validation, Windows-protected token persistence, refresh and sign-out. Real WPF
checks exercise Settings, inline check/identify, follow-ups, Off, suggestion drafts,
unchanged counts/history and stale-response rejection. Fixtures never use personal
history or live ChatGPT credentials.

The installed `qwen2.5:7b-instruct` was exercised with synthetic ギルガメッシュ and
床に本を置いた。 examples. The first prompt representation (escaped Japanese)
produced incorrect identifications and Chinese explanation text. Presenting
Japanese literally improved the tested meanings: it identified Gilgamesh with
historical/fictional uncertainty and chose floor for 床. It still returned romaji
for the name and the wrong とこ reading for floor despite the kana instruction.
This is direct evidence that valid structured output is not a verified reading
or definition. These two examples are not an accuracy benchmark or endorsement
of any installed model. Other installed models have not been linguistically
evaluated, and no model output changes the analyzer's reading automatically.

Live ChatGPT sign-in, account eligibility and model generation require the user to
connect Gengo in the browser. Those are not established by synthetic OAuth tests.
The feature is implemented against the documented preview protocol; future
provider changes can require an adapter update. Morphology and authored grammar
coverage are unchanged by this feature.

## Protocol references

Reviewed 2026-11-04:

- [OpenAI registration and sign-in](https://developers.openai.com/siwc/token-sharing-open-source/sign-in)
- [Accounts, refresh and revocation](https://developers.openai.com/siwc/token-sharing-open-source/profiles-and-sessions)
- [Account model catalog and inference](https://developers.openai.com/siwc/token-sharing-open-source/models-and-inference)
- [ChatGPT-plan preview requirements](https://developers.openai.com/siwc/token-sharing-open-source/preview-limitations)
- [Ollama installed models](https://docs.ollama.com/api/tags)
- [Ollama chat](https://docs.ollama.com/api/chat)
