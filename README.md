# Asynchronous Family Tree Explorer

A C# HTTP client builds an ancestry graph using asynchronous depth-first and breadth-first traversal. Requests are bounded by a shared 30-slot semaphore, and a local sample service supplies synthetic person/family records.

## Engineering focus

- `HttpClient`, `async`/`await`, and `Task.WhenAll` overlap network I/O.
- A semaphore bounds in-flight requests instead of creating unlimited network work.
- DFS tracks visited families to terminate repeated/cyclic family references.
- Concurrent dictionaries safely retain unique people/families when recursive branches complete together.
- The command-line demo verifies retrieved counts against the service and returns a nonzero exit code for incomplete traversals.

## Run the demonstration

Install the .NET 8 SDK. From the repository root, start the sample service in one terminal:

```sh
dotnet run --project src/SampleServer/FamilyTreeServer.csproj --configuration Release
```

In another terminal:

```sh
dotnet run --project src/Explorer/Assignment14.csproj --configuration Release -- --strategy both --generations 3
```

Options: `--strategy dfs|bfs|both`, `--generations 1..6`, and `--help`. Successful runs print the number of people/families and `VERIFIED` when both match the service. Each run initializes a new synthetic tree; do not interpret individual elapsed times as a controlled DFS-versus-BFS benchmark.

The default service URL is `http://127.0.0.1:8123`. Set `FAMILY_TREE_PORT` for a different sample-service port and `FAMILY_TREE_API_URL` for the matching client URL. On Windows, `HttpListener` may require an existing URL reservation for the selected prefix; use a permitted local prefix or run the demo on Linux if the listener reports access denied.

## Validate

```sh
dotnet test tests/Explorer.Tests.csproj --configuration Release
```

Six unit tests cover concurrent/duplicate insertion, missing records, nullable relationship IDs, and zero-ID traversal. Both projects built without warnings during portfolio preparation on October 14, 2026. End-to-end DFS and BFS runs matched sample-service counts at one, two, and three generations; invalid generation input was rejected. CI runs the unit tests and a three-generation end-to-end demonstration.

## Layout

- `src/Explorer`: traversal client, graph models, and logging support.
- `src/SampleServer`: local synthetic HTTP fixture; not a production genealogy service.
- `tests`: focused NUnit correctness tests.

## Limits and provenance

This is a focused extraction of a CSE 351 course project. It includes supporting models/logging and the local sample-service code from the coursework snapshot; the resume contribution should emphasize the asynchronous traversal implementation rather than claiming every support component. See `NOTICE.md`.

The service is intended for one local demo client at a time. Requests use a fixed timeout; cancellation, retries, in-flight request deduplication, production authentication, and a persisted database are future work. The 30-request limit bounds HTTP requests, not operating-system thread count. No distributed deployment or measured speedup is claimed.
