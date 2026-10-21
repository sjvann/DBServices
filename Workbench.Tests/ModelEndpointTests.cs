using System.Net;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Workbench.Adapters;
using Workbench.Core;
using Workbench.Core.Ports;
using Workbench.Web;

namespace Workbench.Tests;

[TestClass]
public class ModelEndpointTests
{
    private const string Key = "KeyMarkerDoNotLeak";
    private const string TypedKey = "TypedMarkerDoNotLeak";
    private const string DefaultAddress = "http://127.0.0.1:11434/v1";

    [TestMethod]
    public async Task Local_blank_address_is_ollama_and_offers_downloads()
    {
        await using var page = new PageSession(new ModelDraft { Enabled = false, Placement = "local" });
        page.Handler.Respond(HttpStatusCode.OK, Models("llama3.2"));

        Assert.AreEqual(DefaultAddress, page.Cut.Find("#model-endpoint").GetAttribute("value"));
        Assert.Contains("本機使用 Ollama", page.Cut.Markup);
        Assert.Contains("下載 llama3.2", page.Cut.Markup);
        Assert.Contains("下載 mistral", page.Cut.Markup);
        Assert.IsFalse(page.World.Text.Contains("11434", StringComparison.Ordinal));

        await Probe(page.Cut);
        Assert.AreEqual(DefaultAddress + "/models", page.Handler.Calls[0].Url);
        page.World.Unchanged();
    }

    [TestMethod]
    public async Task Existing_address_is_not_overwritten()
    {
        await using var page = new PageSession(new ModelDraft
        {
            Enabled = true,
            Placement = "local",
            Endpoint = "http://127.0.0.1:9/v1",
            ModelId = "qwen",
        });
        page.Handler.Respond(HttpStatusCode.OK, Models("qwen"));

        Assert.AreEqual("http://127.0.0.1:9/v1", page.Cut.Find("#model-endpoint").GetAttribute("value"));
        await Probe(page.Cut);
        Assert.AreEqual("http://127.0.0.1:9/v1/models", page.Handler.Calls[0].Url);
        page.World.Unchanged();
    }

    [TestMethod]
    public async Task Listed_models_hide_embeddings_keep_gemma_and_a_click_fills_the_name()
    {
        await using var page = new PageSession(new ModelDraft { Enabled = false, Placement = "local", Endpoint = DefaultAddress });
        page.Handler.Respond(HttpStatusCode.OK, Models(
            "llama3.2",
            "gemma2",
            "nomic-embed-text",
            "text-embedding-3-small",
            "whisper-1",
            "tts-1",
            "dalle-2",
            "omni-moderation",
            "gpt-image-1",
            "Text-Embedding-Ada"));

        await Probe(page.Cut);
        var labels = page.Cut.FindAll("button").Select(button => button.TextContent).ToList();
        Assert.IsTrue(labels.Contains("llama3.2"));
        Assert.IsTrue(labels.Contains("gemma2"));
        Assert.IsFalse(labels.Any(label => Hidden(label)));
        await page.Cut.FindAll("button").Single(button => button.TextContent == "llama3.2").ClickAsync(new MouseEventArgs());
        Assert.AreEqual("llama3.2", page.Cut.Find("#model-id").GetAttribute("value"));
        page.Handler.Respond(HttpStatusCode.OK, Models("llama3.2"));
        await Probe(page.Cut);
        Assert.AreEqual("已連上。llama3.2 可以使用。", page.Cut.Find("#model-result").TextContent);
        page.World.Unchanged();
    }

    [TestMethod]
    public async Task Timeout_and_refusal_stay_human_and_do_not_write_the_file()
    {
        Assert.AreEqual(TimeSpan.FromSeconds(8), ModelEndpoint.Limit);
        using var world = new Bench(new ModelDraft
        {
            Enabled = true,
            Placement = "local",
            Endpoint = DefaultAddress,
            ModelId = "llama3.2",
            ApiKey = Key,
        });
        world.Handler.Throw(new TaskCanceledException("timed out " + Key));
        world.Handler.Throw(new HttpRequestException("refused " + Key));

        var timed = await world.Endpoint.ProbeAsync(Local("llama3.2"), CancellationToken.None);
        var refused = await world.Endpoint.ProbeAsync(Local("llama3.2"), CancellationToken.None);

        Assert.AreEqual("連線逾時，沒有得到回應。", timed.Message);
        Assert.AreEqual("連不上這個位址。", refused.Message);
        Assert.IsFalse(timed.Ok);
        Assert.IsFalse(refused.Ok);
        Assert.IsFalse((timed.Message + refused.Message).Contains(Key, StringComparison.Ordinal));
        world.Unchanged();
    }

    [TestMethod]
    public async Task A_name_missing_from_the_list_still_counts_as_connected()
    {
        await using var page = new PageSession(new ModelDraft
        {
            Enabled = false,
            Placement = "local",
            Endpoint = DefaultAddress,
            ModelId = "llama3.2",
        });
        page.Handler.Respond(HttpStatusCode.OK, Models("Llama3.2"));

        await Probe(page.Cut);

        Assert.Contains("清單沒有這個模型", page.Cut.Find("[role=status]").TextContent);
        Assert.Contains("清單沒有這個模型", page.Cut.Find("#model-result").TextContent);
        Assert.AreEqual("llama3.2", page.Cut.Find("#model-id").GetAttribute("value"));
        Assert.AreEqual("Llama3.2", page.Cut.FindAll("button").Single(button => button.TextContent == "Llama3.2").TextContent);
        page.World.Unchanged();
    }

    [TestMethod]
    public async Task A_tagged_name_is_not_shortened_to_the_text_before_the_colon()
    {
        await using var page = new PageSession(new ModelDraft
        {
            Enabled = false,
            Placement = "local",
            Endpoint = DefaultAddress,
            ModelId = "llama3.2",
        });
        page.Handler.Respond(HttpStatusCode.OK, Models("llama3.2:latest"));

        await Probe(page.Cut);
        Assert.Contains("清單沒有這個模型", page.Cut.Find("[role=status]").TextContent);
        Assert.AreEqual("llama3.2", page.Cut.Find("#model-id").GetAttribute("value"));

        await page.Cut.FindAll("button").Single(button => button.TextContent == "llama3.2:latest").ClickAsync(new MouseEventArgs());
        Assert.AreEqual("llama3.2:latest", page.Cut.Find("#model-id").GetAttribute("value"));
        Assert.IsFalse(page.Cut.Find("[role=status]").TextContent.Contains("清單沒有這個模型", StringComparison.Ordinal));
        Assert.IsTrue(page.Cut.FindAll("button").Any(button => button.TextContent == "llama3.2:latest"));
        page.World.Unchanged();
    }

    [TestMethod]
    [DataRow(404)]
    [DataRow(405)]
    public async Task A_missing_list_sends_one_short_chat_when_the_name_is_filled(int status)
    {
        using var world = new Bench(new ModelDraft
        {
            Enabled = true,
            Placement = "local",
            Endpoint = DefaultAddress + "/models",
            ModelId = "llama3.2:latest",
            ApiKey = Key,
        });
        world.Handler.Respond((HttpStatusCode)status, Key);
        world.Handler.Respond(HttpStatusCode.OK, "{}");

        var result = await world.Service.ProbeModelAsync("admin", new ModelDraft
        {
            Placement = "local",
            Endpoint = DefaultAddress + "/models",
            ModelId = "llama3.2:latest",
        }, CancellationToken.None);

        Assert.IsTrue(result.Ok, result.Error?.Message);
        Assert.AreEqual("已連上，但這個位址沒有模型清單。", result.Value!.Message);
        Assert.AreEqual(0, result.Value.Names.Count);
        Assert.HasCount(2, world.Handler.Calls);
        Assert.AreEqual("GET", world.Handler.Calls[0].Method);
        Assert.AreEqual(DefaultAddress + "/models", world.Handler.Calls[0].Url);
        Assert.AreEqual("Bearer " + Key, world.Handler.Calls[0].Authorization);
        Assert.AreEqual("POST", world.Handler.Calls[1].Method);
        Assert.AreEqual(DefaultAddress + "/chat/completions", world.Handler.Calls[1].Url);
        Assert.Contains("\"model\":\"llama3.2:latest\"", world.Handler.Calls[1].Body);
        Assert.Contains("\"max_tokens\":1", world.Handler.Calls[1].Body);
        Assert.Contains("\"stream\":false", world.Handler.Calls[1].Body);
        Assert.IsLessThan(160, world.Handler.Calls[1].Body.Length);
        Assert.IsFalse(world.Handler.Calls[1].Body.Contains(Key, StringComparison.Ordinal));
        Assert.IsFalse(JsonSerializer.Serialize(result).Contains(Key, StringComparison.Ordinal));
        world.Unchanged();
    }

    [TestMethod]
    [DataRow(404)]
    [DataRow(405)]
    public async Task A_missing_list_without_a_name_does_not_send_chat(int status)
    {
        using var world = new Bench(LocalFile());
        world.Handler.Respond((HttpStatusCode)status, "{}");
        world.Handler.Respond(HttpStatusCode.OK, "{}");

        var result = await world.Endpoint.ProbeAsync(Local(""), CancellationToken.None);

        Assert.IsFalse(result.Ok);
        Assert.AreEqual("請先填模型名稱。", result.Message);
        Assert.HasCount(1, world.Handler.Calls);
        Assert.AreEqual("GET", world.Handler.Calls[0].Method);
        world.Unchanged();
    }

    [TestMethod]
    public async Task Cloud_https_with_a_key_can_be_probed_and_the_page_has_no_download()
    {
        using var world = new Bench(new ModelDraft
        {
            Enabled = true,
            Placement = "cloud",
            Endpoint = "https://api.example/v1",
            ModelId = "gpt",
            ApiKey = Key,
        });
        world.Handler.Respond(HttpStatusCode.OK, Models("gpt"));
        var result = await world.Service.ProbeModelAsync("admin", new ModelDraft
        {
            Placement = "cloud",
            Endpoint = "https://api.example/v1",
            ModelId = "gpt",
            ApiKey = Key,
        }, CancellationToken.None);
        Assert.IsTrue(result.Ok, result.Error?.Message);
        Assert.AreEqual("已連上。", result.Value!.Message);
        Assert.AreEqual("https://api.example/v1/models", world.Handler.Calls[0].Url);
        world.Unchanged();

        await using var page = new PageSession(new ModelDraft
        {
            Enabled = true,
            Placement = "cloud",
            Endpoint = "https://api.example/v1",
            ModelId = "gpt",
            ApiKey = Key,
        });
        Assert.AreEqual("https://api.example/v1", page.Cut.Find("#model-endpoint").GetAttribute("value"));
        Assert.IsFalse(page.Cut.Markup.Contains("下載", StringComparison.Ordinal));
        Assert.IsFalse(page.Cut.Markup.Contains(Key, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Cloud_without_https_or_a_key_does_not_connect()
    {
        using var world = new Bench(new ModelDraft
        {
            Enabled = false,
            Placement = "cloud",
            Endpoint = "https://api.example/v1",
            ModelId = "gpt",
        });
        world.Handler.Respond(HttpStatusCode.OK, Models("gpt"));

        var plain = await world.Endpoint.ProbeAsync(new ModelDraft
        {
            Placement = "cloud",
            Endpoint = "http://api.example/v1",
            ModelId = "gpt",
            ApiKey = Key,
        }, CancellationToken.None);
        var missing = await world.Endpoint.ProbeAsync(new ModelDraft
        {
            Placement = "cloud",
            Endpoint = "https://api.example/v1",
            ModelId = "gpt",
        }, CancellationToken.None);

        Assert.AreEqual("雲端位址必須是 https。", plain.Message);
        Assert.AreEqual("雲端需要金鑰。", missing.Message);
        Assert.HasCount(0, world.Handler.Calls);
        Assert.IsFalse((plain.Message + missing.Message).Contains(Key, StringComparison.Ordinal));
        world.Unchanged();
    }

    [TestMethod]
    public async Task A_non_admin_is_refused_and_a_blank_key_uses_the_file_without_returning_it()
    {
        using var world = new Bench(new ModelDraft
        {
            Enabled = true,
            Placement = "cloud",
            Endpoint = "https://api.example/v1",
            ModelId = "gpt",
            ApiKey = Key,
        });
        world.Handler.Respond(HttpStatusCode.OK, Models("llama3.2", Key));
        world.Handler.Throw(new HttpRequestException("down " + Key));
        world.Handler.Respond(HttpStatusCode.OK, Models("llama3.2"));

        var denied = await world.Service.ProbeModelAsync("user", new ModelDraft
        {
            Placement = "cloud",
            Endpoint = "https://api.example/v1",
            ModelId = "llama3.2",
        }, CancellationToken.None);
        Assert.IsFalse(denied.Ok);
        Assert.AreEqual("沒有權限。", denied.Error!.Message);
        Assert.HasCount(0, world.Handler.Calls);

        var reused = await world.Service.ProbeModelAsync("admin", new ModelDraft
        {
            Placement = "cloud",
            Endpoint = "https://api.example/v1",
            ModelId = "llama3.2",
        }, CancellationToken.None);
        Assert.IsTrue(reused.Ok, reused.Error?.Message);
        Assert.AreEqual("Bearer " + Key, world.Handler.Calls[0].Authorization);
        CollectionAssert.AreEqual(new[] { "llama3.2" }, reused.Value!.Names.ToList());
        Assert.IsFalse(JsonSerializer.Serialize(reused).Contains(Key, StringComparison.Ordinal));

        var failed = await world.Service.ProbeModelAsync("admin", new ModelDraft
        {
            Placement = "cloud",
            Endpoint = "https://api.example/v1",
            ModelId = "llama3.2",
        }, CancellationToken.None);
        Assert.AreEqual("連不上這個位址。", failed.Error!.Message);
        Assert.IsFalse(JsonSerializer.Serialize(failed).Contains(Key, StringComparison.Ordinal));

        var typed = await world.Endpoint.ProbeAsync(new ModelDraft
        {
            Placement = "cloud",
            Endpoint = "https://api.example/v1",
            ModelId = "llama3.2",
            ApiKey = TypedKey,
        }, CancellationToken.None);
        Assert.IsTrue(typed.Ok, typed.Message);
        Assert.AreEqual("Bearer " + TypedKey, world.Handler.Calls[2].Authorization);
        Assert.IsFalse(world.Text.Contains(TypedKey, StringComparison.Ordinal));
        world.Unchanged();
    }

    [TestMethod]
    public async Task A_failed_page_probe_shows_an_alert_and_leaves_the_file()
    {
        await using var page = new PageSession(new ModelDraft
        {
            Enabled = false,
            Placement = "local",
            Endpoint = DefaultAddress,
        });
        page.Handler.Throw(new HttpRequestException("refused"));

        await Probe(page.Cut);

        Assert.Contains("連不上這個位址。", page.Cut.Find("[role=alert]").TextContent);
        Assert.AreEqual("連不上這個位址。", page.Cut.Find("#model-result").TextContent);
        page.World.Unchanged();
    }

    [TestMethod]
    public async Task A_key_typed_on_the_page_is_sent_and_not_written()
    {
        await using var page = new PageSession(new ModelDraft
        {
            Enabled = false,
            Placement = "local",
            Endpoint = DefaultAddress,
            ModelId = "llama3.2",
        });
        page.Handler.Respond(HttpStatusCode.OK, Models("llama3.2"));
        page.Cut.Find("#model-key").Input(TypedKey);

        await Probe(page.Cut);

        Assert.AreEqual("Bearer " + TypedKey, page.Handler.Calls[^1].Authorization);
        Assert.IsFalse(page.World.Text.Contains(TypedKey, StringComparison.Ordinal));
        page.World.Unchanged();
    }

    [TestMethod]
    public async Task A_whitespace_key_reuses_the_stored_key()
    {
        using var world = new Bench(new ModelDraft
        {
            Enabled = true,
            Placement = "cloud",
            Endpoint = "https://api.example/v1",
            ModelId = "gpt",
            ApiKey = Key,
        });
        world.Handler.Respond(HttpStatusCode.OK, Models("gpt"));

        var probed = await world.Endpoint.ProbeAsync(new ModelDraft
        {
            Placement = "cloud",
            Endpoint = "https://api.example/v1",
            ModelId = "gpt",
            ApiKey = "   ",
        }, CancellationToken.None);
        var saved = world.Profile.Save(new ModelDraft
        {
            Enabled = true,
            Placement = "cloud",
            Endpoint = "https://api.example/v1",
            ModelId = "gpt",
            ApiKey = "   ",
        });

        Assert.IsTrue(probed.Ok, probed.Message);
        Assert.AreEqual("Bearer " + Key, world.Handler.Calls[0].Authorization);
        Assert.IsTrue(saved.Ok, saved.Message);
        Assert.IsTrue(world.Text.Contains(Key, StringComparison.Ordinal));
        world.Unchanged();
    }

    [TestMethod]
    public async Task A_local_ftp_address_is_refused_without_a_request()
    {
        using var world = new Bench(LocalFile());
        world.Handler.Respond(HttpStatusCode.OK, Models("llama3.2"));

        var result = await world.Endpoint.ProbeAsync(new ModelDraft
        {
            Placement = "local",
            Endpoint = "ftp://127.0.0.1:11434/v1",
            ModelId = "llama3.2",
        }, CancellationToken.None);

        Assert.AreEqual("本機位址無法使用。", result.Message);
        Assert.HasCount(0, world.Handler.Calls);
        world.Unchanged();
    }

    [TestMethod]
    public async Task Local_page_lists_installed_models_and_a_download_fills_the_installed_name()
    {
        await using var page = new PageSession(
            new ModelDraft { Enabled = false, Placement = "local" },
            Models("llama3.2:latest", "qwen2.5:32b", "gemma4:26b", "dimavz/whisper-tiny:latest"));

        var labels = page.Cut.FindAll("button").Select(button => button.TextContent.Trim()).ToList();
        Assert.Contains("llama3.2:latest", labels);
        Assert.Contains("qwen2.5:32b", labels);
        Assert.Contains("gemma4:26b", labels);
        Assert.IsFalse(labels.Any(label => label.Contains("whisper", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains("下載 llama3.1", labels);
        Assert.Contains("下載 mistral", labels);
        Assert.DoesNotContain("下載 llama3.2", labels);
        Assert.DoesNotContain("下載 qwen2.5", labels);

        page.Handler.Respond(HttpStatusCode.OK, "{\"status\":\"pulling manifest\"}\n{\"status\":\"success\"}\n");
        page.Handler.Respond(HttpStatusCode.OK, Models("llama3.2:latest", "mistral:latest"));
        await page.Cut.FindAll("button").Single(button => button.TextContent.Trim() == "下載 mistral").ClickAsync(new MouseEventArgs());

        Assert.AreEqual("mistral:latest", page.Cut.Find("#model-id").GetAttribute("value"));
        Assert.Contains("還沒有儲存", page.Cut.Find("[role=status]").TextContent);
        var pull = page.Handler.Calls.Single(call => call.Method == "POST");
        Assert.AreEqual("http://127.0.0.1:11434/api/pull", pull.Url);
        Assert.Contains("\"model\":\"mistral\"", pull.Body);
        Assert.Contains("\"stream\":true", pull.Body);
        page.World.Unchanged();
    }

    [TestMethod]
    public async Task Pull_uses_the_native_ollama_path_and_keeps_the_file()
    {
        using var world = new Bench(LocalFile());
        world.Handler.Respond(HttpStatusCode.OK, "{\"status\":\"pulling manifest\"}\n{\"status\":\"success\"}\n");
        world.Handler.Respond(HttpStatusCode.OK, Models("llama3.2:latest"));
        var reported = new Note();

        var result = await world.Service.PullModelAsync("admin", new ModelDraft
        {
            Placement = "local",
            Endpoint = DefaultAddress + "/models",
            ApiKey = Key,
        }, "llama3.2", reported, CancellationToken.None);

        Assert.IsTrue(result.Ok, result.Error?.Message);
        Assert.AreEqual("llama3.2:latest", result.Value!.ModelId);
        Assert.AreEqual("POST", world.Handler.Calls[0].Method);
        Assert.AreEqual("http://127.0.0.1:11434/api/pull", world.Handler.Calls[0].Url);
        Assert.AreEqual("Bearer " + Key, world.Handler.Calls[0].Authorization);
        Assert.IsFalse(world.Handler.Calls[0].Body.Contains(Key, StringComparison.Ordinal));
        Assert.Contains("正在取得模型資訊。", reported.Lines);
        Assert.Contains("已下載。", reported.Lines);
        Assert.IsFalse(JsonSerializer.Serialize(result).Contains(Key, StringComparison.Ordinal));
        world.Unchanged();
    }

    [TestMethod]
    public async Task Pull_refuses_a_name_outside_the_catalog_and_a_non_ollama_address()
    {
        using var world = new Bench(LocalFile());
        world.Handler.Respond(HttpStatusCode.OK, "{}");

        var stranger = await world.Endpoint.PullAsync(Local(""), "llama3.2:latest", null, CancellationToken.None);
        var other = await world.Endpoint.PullAsync(new ModelDraft
        {
            Placement = "local",
            Endpoint = "http://127.0.0.1:9/v1",
        }, "llama3.2", null, CancellationToken.None);

        Assert.AreEqual("這個模型不能從這裡下載。", stranger.Message);
        Assert.AreEqual("只有本機 Ollama 可以下載。", other.Message);
        Assert.HasCount(0, world.Handler.Calls);
        world.Unchanged();
    }

    [TestMethod]
    public async Task A_non_admin_cannot_pull()
    {
        using var world = new Bench(LocalFile());
        var result = await world.Service.PullModelAsync("user", Local(""), "mistral", null, CancellationToken.None);
        Assert.IsFalse(result.Ok);
        Assert.AreEqual("沒有權限。", result.Error!.Message);
        Assert.HasCount(0, world.Handler.Calls);
        world.Unchanged();
    }

    [TestMethod]
    public async Task Cancelling_a_pull_stops_without_writing_the_file()
    {
        using var world = new Bench(LocalFile());
        world.Handler.Hold();
        using var cts = new CancellationTokenSource();
        var pending = world.Endpoint.PullAsync(Local(""), "mistral", null, cts.Token);
        cts.Cancel();
        var result = await pending;
        Assert.AreEqual("已取消下載。", result.Message);
        world.Unchanged();
    }

    [TestMethod]
    public async Task Localhost_counts_as_loopback_and_a_models_suffix_is_not_repeated()
    {
        using var world = new Bench(LocalFile());
        world.Handler.Respond(HttpStatusCode.OK, Models("llama3.2"));
        world.Handler.Respond(HttpStatusCode.OK, Models("llama3.2"));
        world.Handler.Respond(HttpStatusCode.OK, Models("llama3.2"));

        var local = await world.Endpoint.ProbeAsync(new ModelDraft
        {
            Placement = "local",
            Endpoint = "http://localhost:11434/v1",
            ModelId = "llama3.2",
        }, CancellationToken.None);
        var suffixed = await world.Endpoint.ProbeAsync(new ModelDraft
        {
            Placement = "local",
            Endpoint = DefaultAddress + "/models",
            ModelId = "llama3.2",
        }, CancellationToken.None);
        var remote = await world.Endpoint.ProbeAsync(new ModelDraft
        {
            Placement = "local",
            Endpoint = "http://10.0.0.8:11434/v1",
            ModelId = "llama3.2",
        }, CancellationToken.None);

        Assert.IsTrue(local.Ok, local.Message);
        Assert.AreEqual("http://localhost:11434/v1/models", world.Handler.Calls[0].Url);
        Assert.AreEqual(DefaultAddress + "/models", world.Handler.Calls[1].Url);
        Assert.AreEqual("本機位址必須在這台機器上。", remote.Message);
        Assert.HasCount(2, world.Handler.Calls);
        world.Unchanged();
    }

    [TestMethod]
    public async Task Host_starts_with_the_fake_endpoint()
    {
        await using var host = new WorkbenchHost();
        using var client = host.CreateClient();
        var endpoint = host.Services.GetRequiredService<IModelEndpoint>();
        var model = host.Services.GetRequiredService<IModelProfile>();

        Assert.IsInstanceOfType<RecordingModelEndpoint>(endpoint);
        Assert.AreEqual("", model.ResolveKey(null));
        Assert.AreEqual("typed", model.ResolveKey("typed"));
        var response = await client.GetAsync("/");
        Assert.IsTrue(response.IsSuccessStatusCode);
    }

    private sealed class Note : IProgress<string>
    {
        public List<string> Lines { get; } = [];
        public void Report(string value) => Lines.Add(value);
    }

    private static ModelDraft Local(string modelId) => new()
    {
        Placement = "local",
        Endpoint = DefaultAddress,
        ModelId = modelId,
    };

    private static ModelDraft LocalFile() => new()
    {
        Enabled = true,
        Placement = "local",
        Endpoint = DefaultAddress,
        ModelId = "llama3.2",
    };

    private static string Models(params string[] names)
    {
        var data = string.Join(",", names.Select(name => "{\"id\":" + JsonSerializer.Serialize(name) + "}"));
        return "{\"data\":[" + data + "]}";
    }

    private static bool Hidden(string name)
    {
        string[] tokens = ["embed", "embedding", "whisper", "tts", "dalle", "moderation", "gpt-image", "text-embedding"];
        return tokens.Any(token => name.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task Probe(IRenderedComponent<Workbench.Web.Components.Environment> cut)
    {
        var button = cut.Find("#model-endpoint").Closest("form")!.QuerySelectorAll("button")
            .Single(item => item.TextContent.Contains("測試連線", StringComparison.Ordinal));
        await button.ClickAsync(new MouseEventArgs());
    }

    private sealed class PageSession : IAsyncDisposable
    {
        private readonly BunitContext _context = new();

        public PageSession(ModelDraft stored, string? installed = null)
        {
            World = new Bench(stored);
            var placement = string.IsNullOrEmpty(stored.Placement) ? "local" : stored.Placement;
            if (placement == "local")
                World.Handler.Respond(HttpStatusCode.OK, installed ?? "{\"data\":[]}");
            var store = new MemoryOperators();
            store.Add(new OperatorRecord { Id = "admin", DisplayName = "管理者", IsAdmin = true });
            var sessions = new MemorySessionStore();
            var hasher = new Pbkdf2PasswordHasher();
            var sessionService = new SessionService(store, hasher, sessions, new SeedAdminService(store, hasher));
            var circuit = new CircuitSession();
            circuit.Adopt(sessions.Create("admin"));
            _context.Services.AddSingleton(new OperatorGate(circuit, sessionService, new Profiles(store)));
            _context.Services.AddSingleton(World.Service);
            Cut = _context.Render<Workbench.Web.Components.Environment>();
        }

        public Bench World { get; }
        public ScriptHandler Handler => World.Handler;
        public IRenderedComponent<Workbench.Web.Components.Environment> Cut { get; }

        public async ValueTask DisposeAsync()
        {
            await _context.DisposeAsync();
            World.Dispose();
        }
    }

    private sealed class Bench : IDisposable
    {
        private readonly string _before;

        public Bench(ModelDraft? stored = null)
        {
            var path = Path.Combine(Path.GetTempPath(), "wb-probe-" + Guid.NewGuid().ToString("N") + ".json");
            File = path;
            Profile = new FileModelProfile(path);
            if (stored is not null)
            {
                var saved = Profile.Save(stored);
                if (!saved.Ok)
                    throw new InvalidOperationException(saved.Message);
            }

            _before = Text;
            Handler = new ScriptHandler();
            Endpoint = new ModelEndpoint(Profile, Handler);
            var operators = new MemoryOperators();
            operators.Add(new OperatorRecord { Id = "admin", DisplayName = "管理者", IsAdmin = true });
            operators.Add(new OperatorRecord { Id = "user", DisplayName = "使用者", IsAdmin = false });
            Service = new EnvironmentService(
                new RecordingConnections(),
                new RecordingConnectionAdmin(),
                Profile,
                Endpoint,
                new Profiles(operators));
        }

        public string File { get; }
        public FileModelProfile Profile { get; }
        public ScriptHandler Handler { get; }
        public ModelEndpoint Endpoint { get; }
        public EnvironmentService Service { get; }
        public string Text => System.IO.File.Exists(File) ? System.IO.File.ReadAllText(File) : "";

        public void Unchanged() => Assert.AreEqual(_before, Text);

        public void Dispose()
        {
            if (System.IO.File.Exists(File))
                System.IO.File.Delete(File);
        }
    }

    private sealed class ScriptHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _steps = new();

        public List<Call> Calls { get; } = [];

        public void Respond(HttpStatusCode status, string body) => _steps.Enqueue(async (request, token) =>
        {
            await Remember(request, token);
            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        });

        public void Throw(Exception exception) => _steps.Enqueue(async (request, token) =>
        {
            await Remember(request, token);
            await Task.Yield();
            throw exception;
        });

        public void Hold() => _steps.Enqueue(async (request, token) =>
        {
            await Remember(request, token);
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_steps.Count == 0)
                throw new InvalidOperationException("沒有下一個回應。");
            return _steps.Dequeue()(request, cancellationToken);
        }

        private async Task Remember(HttpRequestMessage request, CancellationToken token)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(token);
            var authorization = request.Headers.TryGetValues("Authorization", out var values) ? string.Join(" ", values) : "";
            Calls.Add(new Call(request.Method.Method, request.RequestUri!.AbsoluteUri, authorization, body));
        }
    }

    private sealed record Call(string Method, string Url, string Authorization, string Body);

    private sealed class MemoryOperators : IOperatorStore
    {
        private readonly List<OperatorRecord> _rows = [];

        public int Count() => _rows.Count;
        public void Add(OperatorRecord record) => _rows.Add(record);
        public OperatorRecord? FindByName(string name) => _rows.FirstOrDefault(row => row.DisplayName == name);
        public OperatorRecord? FindById(string id) => _rows.FirstOrDefault(row => row.Id == id);
        public IReadOnlyList<OperatorRecord> List() => _rows.ToList();
        public void ReplaceEntries(string operatorId, IReadOnlyList<string> entries) { }
        public IReadOnlyList<string> EntriesFor(string operatorId) => [];
    }
}
