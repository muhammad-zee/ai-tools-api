# Globulars — AI Portfolio Build Plan
### Stack: .NET Core + Angular + Ollama (local, free) → swappable to OpenAI/Anthropic for clients

---

## The one idea that makes this whole plan work

Ollama exposes an **OpenAI-compatible API** at `http://localhost:11434/v1`.

That means in .NET you use the **same code** whether you're running a free local Llama model for practice or a paid OpenAI/Anthropic model for a real client. You only change the endpoint URL and the API key. So:

- **Practice / demos** → point at local Ollama, costs nothing.
- **Client production** → point at OpenAI or Anthropic, better quality.

Use `Microsoft.Extensions.AI` or **Semantic Kernel** as the abstraction layer — both have Ollama connectors and both are Microsoft-native, which is a selling point for enterprise/.NET clients.

**Local models to pull:**
- `llama3.1` — general chat + supports tool/function calling (needed for agents)
- `nomic-embed-text` — embeddings (needed for RAG)
- `qwen2.5-coder` — if you want a coding-focused model

**One honest limitation:** local models are weaker than GPT/Claude at complex reasoning and reliable JSON. Perfect for learning and demos; for a paying client, quote the paid API. Design everything so the swap is trivial.

---

## Project 1 — Structured Document Extraction API
**Time: 2–3 days · Difficulty: entry**

### What it does
User uploads a messy document (invoice, resume, purchase order, contract). The AI returns clean, validated **structured JSON** — specific fields pulled out reliably.

### Business pitch
"Automate your manual data entry. Stop paying people to retype invoices into your system."
Almost every business with paperwork wants this. Easy first sale.

### Stack & technicalities
- **Backend:** .NET Core Web API. Endpoint `POST /extract` accepting a file.
- **Text extraction:** parse PDF/image to text first (e.g. a PDF text library; OCR later if needed).
- **AI call:** send the text with a prompt like *"Extract the following fields and return only JSON matching this schema…"*
- **Key concept — Structured Outputs:** define a C# class (e.g. `Invoice` with `InvoiceNumber`, `Date`, `Total`, `LineItems[]`), pass it as the expected schema, deserialize the model's JSON response straight into that class.
- **Frontend:** Angular page — file upload, then show extracted fields in an editable form.

### "Done" looks like
Upload an invoice → get back correct structured fields displayed in the UI → deserializes into a strongly-typed C# object without errors.

### Skill learned
Prompting for structured data + reliable JSON parsing. The foundation for everything below.

---

## Project 2 — RAG Document Assistant  ⭐ FLAGSHIP
**Time: 4–5 days · Difficulty: core**

### What it does
A chatbot that answers questions over the client's **own documents**. Upload PDFs → ask "What's our refund policy?" → it answers using only those documents, with sources.

### Business pitch
"An AI assistant that knows your company's data — policies, manuals, contracts, product docs." This is the **single most-requested paid AI job right now.** Spend the most time here.

### Stack & technicalities
This is the important one, so here's the full flow:

1. **Ingestion:** user uploads PDFs → extract text → **chunk** it (split into ~500–1000 token pieces with slight overlap).
2. **Embeddings:** send each chunk to `nomic-embed-text` (via Ollama) → get a vector (array of floats) per chunk.
3. **Vector store:** save chunks + vectors. Use **pgvector** (a Postgres extension) — natural fit since you already know SQL. Alternative: **Qdrant** in Docker (free, local).
4. **Retrieval:** when the user asks a question → embed the question → find the most similar chunks by vector distance (cosine similarity).
5. **Generation:** stuff those retrieved chunks into the prompt as context → *"Answer using only this context: {chunks}"* → model answers grounded in real data.
6. **Cite sources:** return which document/chunk the answer came from. Clients love this — it builds trust.

- **Backend:** .NET Core. Endpoints: `POST /ingest`, `POST /chat`.
- **Frontend:** Angular chat interface. Add **streaming** responses (token-by-token) — looks professional in a live demo.

### "Done" looks like
Upload a company handbook → ask a question → get a correct answer that cites the source page. Ask about something not in the docs → it says it doesn't know (doesn't hallucinate).

### Skill learned
Embeddings, vector search, retrieval, grounding. This is what "AI engineer" means to most clients in 2026.

---

## Project 3 — Multi-Step AI Agent
**Time: 3–4 days · Difficulty: intermediate**

### What it does
An AI that **reasons and acts across multiple steps** using tools you give it. Example: a research assistant that takes a topic, decides to call a web-search function, reads the results, maybe searches again, then writes a structured report — choosing its own steps.

### Business pitch
"Automate multi-step workflows your team does by hand." Now you legitimately earn the word **"agent"** you're already advertising.

### Stack & technicalities
- **Framework:** **Semantic Kernel** (.NET-native, enterprise-friendly). This is where it shines.
- **Key concept — Tool / Function Calling:** you expose C# methods (e.g. `SearchWeb(query)`, `SaveReport(text)`) to the model. The model *decides* when to call them, you execute them, feed results back, and it continues. This maps perfectly onto your backend skills — you're wiring the model into real functions.
- **The agent loop:** model reasons → picks a tool → you run it → return the result → repeat until it produces a final answer.
- **Use local `llama3.1`** — it supports tool calling. (For a real client, GPT/Claude are more reliable at this.)
- **Frontend:** Angular — show the agent's steps live ("Searching… Reading… Writing report…"). Showing the reasoning is impressive in demos.

### "Done" looks like
Give it a goal → watch it call tools in sequence on its own → produce a useful result. It picks the steps, not you.

### Skill learned
Function calling + agentic loops. The real version of "AI agents," not just integration.

---

## Project 4 — AI-Powered Mobile Feature (optional, your differentiator)
**Time: 3–4 days · Note: this one is Flutter**

You said you're focusing on .NET + Angular, so treat this as **optional** — but it's where you and your partners (Farhan & Nasir, Flutter devs) stand out, since most Upwork AI freelancers can't do polished cross-platform mobile. If you skip Flutter, you can build the same idea as an **Angular PWA / mobile-responsive web app** instead.

### What it does
A mobile (or mobile-web) app with a genuinely useful AI feature talking to your .NET AI backend — e.g. voice notes → structured summary, or snap a photo → AI describes/analyzes it.

### Business pitch
"AI-powered apps across web *and* mobile." Makes Globulars distinct from single-platform freelancers.

### Skill learned
Connecting a mobile/PWA front end to your AI backend — the full product story.

---

## Extra projects worth adding (pick based on target clients)

**5 — AI Workflow Automation ("if-this-then-AI")**
Watches an inbox/folder → AI classifies or drafts a response → routes it. Sell as: *"AI that handles your repetitive email/ticket triage."* Backend-heavy, pure .NET, huge SME demand.

**6 — Natural-Language-to-SQL Reporting**
User asks "How many orders last month from Lahore?" in plain English → AI writes the SQL → runs it against their DB → returns the answer/chart. Sell as: *"Let non-technical staff query your database in plain language."* Plays directly to your SQL Server + .NET strength. Very impressive in demos. **Safety note:** run generated queries read-only / sandboxed.

**7 — Customer-Support Chatbot Widget**
Embeddable chat widget (RAG-backed, from Project 2) that any business can drop on their website. Sell as a **productized, recurring-revenue** offer — this is the easiest thing to package and resell repeatedly.

---

## How to use these when pitching a business

Walk into any company and you now have three concrete offers:
1. **"We can automate this"** → Projects 1, 5, 6 (data entry, email triage, reporting).
2. **"We can integrate AI into your existing system"** → Projects 2, 7 (a chatbot that knows their data).
3. **"We can build you an AI-based system from scratch"** → Projects 3, 4 (agents, AI apps).

**Strong move:** build all of them around **one fictional client in a single vertical** (e.g. a healthcare clinic or a logistics firm). Then your portfolio reads as *"here's how we AI-enabled one business end to end"* — far more convincing to a real prospect in that industry than scattered tech demos.

---

## Suggested 2-week sequence (parallel with client outreach)

| Days | Build | Also doing (mornings) |
|------|-------|----------------------|
| 1–3 | Project 1 (extraction) | Rewrite Upwork profiles around AI |
| 4–8 | Project 2 (RAG) ⭐ | Warm-message past clients |
| 9–12 | Project 3 (agent) | Start selective AI-job bidding |
| 13–14 | Polish + record demo videos | Update LinkedIn |

Record a **short screen-recording of each demo** — you can send those in Upwork proposals and DMs without the client needing to run anything.

---

*Ollama setup quick ref:*
```
ollama pull llama3.1
ollama pull nomic-embed-text
# .NET points at:  http://localhost:11434/v1  (OpenAI-compatible)
# Swap to OpenAI/Anthropic for clients by changing endpoint + key only
```
