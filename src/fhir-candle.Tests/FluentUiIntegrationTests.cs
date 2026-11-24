// <copyright file="FluentUiIntegrationTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

extern alias candleR4;
extern alias coreR4;

using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using fhir.candle.Layout;
using fhir.candle.Pages.Smart;
using fhir.candle.Services;
using fhir.candle.Ui;
using FhirCandle.Configuration;
using FhirCandle.Models;
using FhirCandle.Smart;
using FhirCandle.Storage;
using FhirCandle.Ui;
using FhirCandle.Ui.Common.Components;
using FhirCandle.Ui.R4.Subscriptions;
using FhirCandle.Utils;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;
using PatientR4 = coreR4::Hl7.Fhir.Model.Patient;

namespace fhir.candle.Tests;

/// <summary>Real managed component bindings and interop ordering, not browser/visual qualification.</summary>
public class FluentUiIntegrationTests
{
    /// <summary>The root owns the outlets, inherited theme, drawer, notifications, and listener lifetime.</summary>
    [Fact]
    public async Task RootOwnsProvidersAndForwardsThemeLifecycle()
    {
        RecordingJs js = new() { DelaySnapshot = true };
        using ServiceProvider services = Services(js);
        await using ManagedRenderer renderer = new(services);
        CandleThemeState state = services.GetRequiredService<CandleThemeState>();
        await renderer.MountAsync<CandleUiRoot>(new()
        {
            [nameof(CandleUiRoot.ChildContent)] = LayoutContent(),
        });
        Assert.Single(renderer.Components<CandleUiRoot>());
        Assert.Single(renderer.Components<FluentProviders>());
        Assert.Single(renderer.Components<FluentDialogProvider>());
        Assert.Single(renderer.Components<FluentToastProvider>());
        Assert.Single(renderer.Components<FluentTooltipProvider>());
        Assert.Equal("MESSAGES_TOP", Assert.Single(renderer.Components<FluentMessageBarProvider>()).Section);
        renderer.AssertFeedbackBeforeLayout();
        string css = File.ReadAllText(SourcePath("../fhir-candle/wwwroot/css/candle-theme.css"));
        string region = css.Split(".candle-messages-top {", StringSplitOptions.None)[1].Split('}')[0];
        Assert.Contains("position: fixed", region);
        Assert.Contains("inset: 0 1rem auto", region);
        Assert.Contains("max-height: 40dvh", region);
        Assert.Contains("overflow-y: auto", region);
        Assert.Contains("z-index: 100", region);
        Assert.DoesNotContain(js.Calls, call => call.Name.StartsWith("Blazor.theme", StringComparison.Ordinal));
        Assert.DoesNotContain(js.Calls, call => call.Name == "candleTheme.persist");

        MainLayout layout = Assert.Single(renderer.Components<MainLayout>());
        INavTracker tracker = layout;
        int notifications = 0;
        tracker.OnThemeChanged += (_, _) => notifications++;
        FluentButton toggle = renderer.Components<FluentButton>().Single(button => button.Title == "Light/Dark Mode");
        await renderer.Dispatcher.InvokeAsync(() => toggle.OnClick.InvokeAsync(new MouseEventArgs()));
        Assert.True(tracker.IsDarkMode);
        Assert.Equal(1, notifications);
        Assert.DoesNotContain(js.Calls, call => call.Name == "candleTheme.persist");

        js.Snapshot.SetResult(new CandleUiRoot.BrowserSnapshot()
        {
            Id = 7,
            Json = """{"mode":"light","primaryColor":"#123456","neutralColor":"#abc","future":{"keep":true}}""",
            IsSystemDark = false,
        });
        await EventuallyAsync(renderer, () => js.Calls.Any(call => call.Name == "candleTheme.persist"));
        Assert.True(tracker.IsDarkMode);
        Assert.Contains("\"future\"", state.Preference.ToJson());
        Assert.Equal(1, notifications);
        Assert.All(js.Calls.Where(call => call.Name.StartsWith("Blazor.theme", StringComparison.Ordinal)),
            call => Assert.Equal("Blazor.theme.setBrandThemeToElementFromSettings", call.Name));
        JsCall theme = js.Calls.Last(call => call.Name.StartsWith("Blazor.theme", StringComparison.Ordinal));
        Assert.IsType<ElementReference>(theme.Args[0]);
        Dictionary<string, object?> settings = Assert.IsType<Dictionary<string, object?>>(theme.Args[1]);
        Assert.Equal("dark", settings["mode"]);
        Assert.Equal("#123456", settings["color"]);

        // The layout button publishes through the real dialog service into this root's provider.
        FluentButton navigation = renderer.Components<FluentButton>().Single(button => button.Title == "Navigation");
        Task drawer = renderer.Dispatcher.InvokeAsync(() => navigation.OnClick.InvokeAsync(new MouseEventArgs()));
        await EventuallyAsync(renderer, () => renderer.Components<NavMenu>().Any());
        NavMenu menu = Assert.Single(renderer.Components<NavMenu>());
        Assert.Equal("FHIR Candle", menu.DialogInstance.Options.Header.Title);
        Assert.Equal(DialogAlignment.Start, menu.DialogInstance.Options.Alignment);
        Assert.False(menu.DialogInstance.Options.Header.CloseAction.Visible);
        Assert.False(menu.DialogInstance.Options.Footer.PrimaryAction.Visible);
        Assert.False(menu.DialogInstance.Options.Footer.SecondaryAction.Visible);
        Assert.NotEqual(true, menu.DialogInstance.Options.PreventDismissOnEscape);
        Assert.All(renderer.Components<FluentNavCategory>(), category => Assert.True(category.Expanded));
        Assert.Contains(renderer.Components<FluentNavItem>(), item => item.Href == "/store?store=test");
        renderer.AssertIconRendered();
        await renderer.Dispatcher.InvokeAsync(() => menu.DialogInstance.CancelAsync());
        await drawer;

        CandleUiRoot root = Assert.Single(renderer.Components<CandleUiRoot>());
        await renderer.Dispatcher.InvokeAsync(() => root.SystemChanged(true));
        Assert.Equal(1, notifications); // An explicit choice ignores system changes.
        js.PersistGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await renderer.Dispatcher.InvokeAsync(() => toggle.OnClick.InvokeAsync(new MouseEventArgs()));
        await EventuallyAsync(renderer, () => js.Calls.Count(call => call.Name == "candleTheme.persist") == 2);
        Assert.False(tracker.IsDarkMode);
        Assert.Equal(2, notifications);
        await renderer.Dispatcher.InvokeAsync(() => toggle.OnClick.InvokeAsync(new MouseEventArgs()));
        Assert.True(tracker.IsDarkMode);
        Assert.Equal(3, notifications);
        js.PersistGate.SetResult();
        await EventuallyAsync(renderer, () => js.Calls.Count(call => call.Name == "candleTheme.persist") == 3);
        long[] revisions = js.Calls.Where(call => call.Name == "candleTheme.persist")
            .Select(call => (long)call.Args[2]!).ToArray();
        Assert.Equal([1L, 2L, 3L], revisions);
        await renderer.DisposeAsync();
        Assert.Single(js.Calls, call => call.Name == "candleTheme.disconnect");
        DotNetObjectReference<CandleUiRoot> callback = Assert.IsType<DotNetObjectReference<CandleUiRoot>>(
            js.Calls.Single(call => call.Name == "candleTheme.connect").Args[0]);
        Assert.Throws<ObjectDisposedException>(() => callback.Value);
        int calls = js.Calls.Count;
        state.Toggle();
        Assert.Equal(calls, js.Calls.Count);
        Assert.Equal(3, notifications);

        RecordingJs systemJs = new()
        {
            InitialSnapshot = new() { Id = 8, Json = """{"mode":"system"}""", IsSystemDark = true },
        };
        using ServiceProvider systemServices = Services(systemJs);
        await using ManagedRenderer systemRenderer = new(systemServices);
        await systemRenderer.MountAsync<CandleUiRoot>(new() { [nameof(CandleUiRoot.ChildContent)] = LayoutContent() });
        MainLayout systemLayout = Assert.Single(systemRenderer.Components<MainLayout>());
        Assert.True(systemLayout.IsDarkMode);
        Assert.DoesNotContain(systemJs.Calls, call => call.Name == "candleTheme.persist");
        int systemNotifications = 0;
        systemLayout.OnThemeChanged += (_, _) => systemNotifications++;
        await systemRenderer.Dispatcher.InvokeAsync(() => Assert.Single(systemRenderer.Components<CandleUiRoot>()).SystemChanged(false));
        Assert.False(systemLayout.IsDarkMode);
        Assert.Equal(1, systemNotifications);
        Assert.DoesNotContain(systemJs.Calls, call => call.Name == "candleTheme.persist");

        RecordingJs pendingJs = new() { DelaySnapshot = true };
        using ServiceProvider pendingServices = Services(pendingJs);
        await using ManagedRenderer pendingRenderer = new(pendingServices);
        await pendingRenderer.MountAsync<CandleUiRoot>([]);
        ValueTask cleanup = pendingRenderer.DisposeAsync();
        pendingJs.Snapshot.SetResult(new() { Id = 9 });
        await cleanup;
        Assert.Single(pendingJs.Calls, call => call.Name == "candleTheme.disconnect");
        Assert.DoesNotContain(pendingJs.Calls, call => call.Name == "candleTheme.persist");
    }

    /// <summary>The complete vendor anchor event dismisses only its owning service-created drawer.</summary>
    [Theory]
    [InlineData("/")]
    [InlineData("/smart/clients")]
    [InlineData("/store?store=test")]
    [InlineData("/subscriptions/local?store=test")]
    [InlineData("/subscriptions/notifications-received?store=test")]
    public async Task DrawerDestinationDismissesOwningDialog(string href)
    {
        using ServiceProvider services = Services(new RecordingJs());
        await using ManagedRenderer renderer = new(services);
        (NavMenu menu, Task closed) = await OpenDrawerAsync(renderer);
        FluentDialogProvider provider = Assert.Single(renderer.Components<FluentDialogProvider>());
        FluentNavCategory category = Assert.Single(renderer.Components<FluentNavCategory>());
        await renderer.ClickAsync(category, "button");
        Assert.False(category.Expanded);
        Assert.Single(renderer.Components<NavMenu>());
        await renderer.ClickAsync(category, "button");
        Assert.True(category.Expanded);

        FluentNavItem item = renderer.Components<FluentNavItem>().Single(item => item.Href == href);
        renderer.AssertDefaultAnchor(item, href);
        TestNavigation navigation = Assert.IsType<TestNavigation>(services.GetRequiredService<NavigationManager>());
        // DispatchEventAsync awaits FluentNavItem's continuation AFTER our callback cancels the dialog.
        await renderer.ClickAsync(item, "a");
        renderer.ThrowErrors();
        Assert.Empty(renderer.Components<NavMenu>());
        Assert.Empty(renderer.Components<FluentDialog>());
        Assert.Same(provider, Assert.Single(renderer.Components<FluentDialogProvider>()));
        await closed;
        Assert.Equal(0, navigation.NavigateCalls);
        Assert.NotEqual(true, menu.DialogInstance.Options.PreventDismissOnEscape);
    }

    /// <summary>The released body places the configured title and version in drawer title/footer slots.</summary>
    [Fact]
    public async Task DrawerRendersConfiguredTitleAndVersionFooter()
    {
        using ServiceProvider services = Services(new RecordingJs());
        await using ManagedRenderer renderer = new(services);
        (NavMenu menu, Task closed) = await OpenDrawerAsync(renderer);
        renderer.AssertDrawerPresentation(menu);
        Assert.False(menu.DialogInstance.Options.Header.CloseAction.Visible);
        Assert.False(menu.DialogInstance.Options.Footer.PrimaryAction.Visible);
        Assert.False(menu.DialogInstance.Options.Footer.SecondaryAction.Visible);
        await renderer.Dispatcher.InvokeAsync(() => menu.DialogInstance.CancelAsync());
        await closed;
    }

    /// <summary>Every tenant destination renders its own icon, even when Fluent suppresses subitem IconRest.</summary>
    [Fact]
    public async Task DrawerRendersEachTenantLinkIcon()
    {
        using ServiceProvider services = Services(new RecordingJs());
        await using ManagedRenderer renderer = new(services);
        (NavMenu menu, Task closed) = await OpenDrawerAsync(renderer);
        foreach (string href in new[]
        {
            "/store?store=test", "/subscriptions/local?store=test",
            "/subscriptions/notifications-received?store=test",
        })
        {
            FluentNavItem item = renderer.Components<FluentNavItem>().Single(item => item.Href == href);
            renderer.AssertLinkIcon(item, href);
        }
        await renderer.Dispatcher.InvokeAsync(() => menu.DialogInstance.CancelAsync());
        await closed;
    }

    /// <summary>Search and row activation exercise the picker's bound callbacks, including clear and multi-select.</summary>
    [Fact]
    public async Task PickerCallbacksPreserveFilteringAndSelection()
    {
        using IFhirStore store = new candleR4::FhirCandle.Storage.VersionedFhirStore();
        store.Init(new TenantConfiguration()
        {
            FhirVersion = FhirReleases.FhirSequenceCodes.R4,
            ControllerName = "picker",
            BaseUrl = "http://localhost/fhir/picker",
            AllowExistingId = true,
        });
        foreach (string id in new[] { "alpha", "bravo" })
        {
            FhirRequestContext request = new(store, "POST", store.Config.BaseUrl + "/Patient")
            {
                SourceFormat = "application/fhir+json",
                SourceContent = $$"""{"resourceType":"Patient","id":"{{id}}","name":[{"family":"{{id}}"}]}""",
            };
            Assert.True(store.InstanceCreate(request, out _, forceAllowExistingId: true));
        }

        IResourceStore patientStore = store["Patient"];
        RecordingJs js = new();
        using ServiceProvider services = Services(js);
        await using ManagedRenderer pickerRenderer = new(services);
        List<string[]> selections = [];
        await pickerRenderer.MountAsync<ResourcePicker>(new()
        {
            [nameof(ResourcePicker.Store)] = store,
            [nameof(ResourcePicker.ResourceStore)] = patientStore,
            [nameof(ResourcePicker.ResourceType)] = "Patient",
            [nameof(ResourcePicker.SelectedIdsChanged)] = EventCallback.Factory.Create<HashSet<string>>(
                this, ids => selections.Add(ids.Order().ToArray())),
        });
        FluentDataGrid<InstanceTableRec> grid = Assert.Single(pickerRenderer.Components<FluentDataGrid<InstanceTableRec>>());
        Assert.True(grid.OnRowClick.HasDelegate);
        Assert.False(grid.OnRowFocus.HasDelegate);
        await Activate("alpha");
        await HeaderPreservesSelection();
        await Activate("bravo");
        Assert.Equal(["bravo"], selections.Last());
        await pickerRenderer.UpdateAsync(new() { [nameof(ResourcePicker.AllowMultipleSelection)] = true });
        await Activate("alpha");
        Assert.Equal(["alpha", "bravo"], selections.Last());
        await Activate("bravo");
        Assert.Equal(["alpha"], selections.Last());
        await HeaderPreservesSelection();

        await pickerRenderer.Dispatcher.InvokeAsync(() => grid.ShowColumnOptionsAsync(0));
        FluentTextInput search = pickerRenderer.Components<FluentTextInput>()
            .Single(input => input.Placeholder == "Resource ID...");
        Assert.True(search.Immediate);
        Assert.Equal(0, search.ImmediateDelay);
        Assert.Equal(TextInputType.Search, search.TextInputType);
        Assert.True(search.ValueChanged.HasDelegate);
        await pickerRenderer.Dispatcher.InvokeAsync(() => search.ValueChanged.InvokeAsync("ALPHA"));
        await EventuallyAsync(pickerRenderer, () => grid.Items?.Count() == 1);
        Assert.Equal("alpha", Assert.Single(grid.Items!).Id);
        await pickerRenderer.Dispatcher.InvokeAsync(() => search.ValueChanged.InvokeAsync(string.Empty));
        // Clear is immediate, not another debounce interval.
        Assert.Equal(2, grid.Items!.Count());
        Assert.Equal(["alpha"], selections.Last());

        ResourcePicker mountedPicker = Assert.Single(pickerRenderer.Components<ResourcePicker>());
        const string createdEvent = nameof(IResourceStore.OnInstanceCreated);
        const string deletedEvent = nameof(IResourceStore.OnInstanceDeleted);
        Delegate[] createdBaseline = GetPatientEventHandlers(createdEvent);
        Delegate[] deletedBaseline = GetPatientEventHandlers(deletedEvent);
        Assert.Contains(createdBaseline, handler => ReferenceEquals(handler.Target, mountedPicker));
        Assert.Contains(deletedBaseline, handler => ReferenceEquals(handler.Target, mountedPicker));

        Dictionary<string, object?> hti2Parameters = new()
        {
            [nameof(CandleUiRoot.ChildContent)] = LayoutContent(builder =>
            {
                builder.OpenComponent<UsCoreHti2ContentsR4>(0);
                builder.AddAttribute(1, nameof(UsCoreHti2ContentsR4.Store), store);
                builder.AddAttribute(2, nameof(UsCoreHti2ContentsR4.StoreName), "picker");
                builder.CloseComponent();
            }),
        };
        UsCoreHti2ContentsR4 firstPage;
        await using (ManagedRenderer firstHti2Renderer = new(services))
        {
            await firstHti2Renderer.MountAsync<CandleUiRoot>(hti2Parameters);
            firstPage = Assert.Single(firstHti2Renderer.Components<UsCoreHti2ContentsR4>());
            FluentAutocomplete<PatientR4, string> patients =
                Assert.Single(firstHti2Renderer.Components<FluentAutocomplete<PatientR4, string>>());
            Assert.False(patients.Multiple);
            Assert.True(patients.OnOptionsSearch.HasDelegate);
            Assert.True(patients.SelectedItemsChanged.HasDelegate);
            OptionsSearchEventArgs<PatientR4> patientSearch = new() { Text = "ALP" };
            await firstHti2Renderer.Dispatcher.InvokeAsync(() => patients.OnOptionsSearch.InvokeAsync(patientSearch));
            PatientR4 patient = Assert.Single(patientSearch.Items!);
            Assert.Equal("alpha", patient.Id);
            await firstHti2Renderer.Dispatcher.InvokeAsync(() => patients.SelectedItemsChanged.InvokeAsync([patient]));
            Assert.Equal("alpha", Assert.Single(patients.SelectedItems).Id);
            await firstHti2Renderer.Dispatcher.InvokeAsync(() => patients.SelectedItemsChanged.InvokeAsync([]));
            Assert.Empty(patients.SelectedItems);
            FluentAutocomplete<ParsedSubscription, string> subscriptions =
                Assert.Single(firstHti2Renderer.Components<FluentAutocomplete<ParsedSubscription, string>>());
            OptionsSearchEventArgs<ParsedSubscription> subscriptionSearch = new() { Text = "missing" };
            await firstHti2Renderer.Dispatcher.InvokeAsync(() => subscriptions.OnOptionsSearch.InvokeAsync(subscriptionSearch));
            Assert.Empty(subscriptionSearch.Items!);
            FluentSelect<string, string> status = firstHti2Renderer.Components<FluentSelect<string, string>>()
                .Single(select => select.Label == "Status");
            await firstHti2Renderer.Dispatcher.InvokeAsync(() => status.ValueChanged.InvokeAsync("active"));
            Assert.Equal("active", status.Value);
        }
        AssertPatientEventsAfterDisposal(1, firstPage);

        UsCoreHti2ContentsR4 secondPage;
        await using (ManagedRenderer secondHti2Renderer = new(services))
        {
            await secondHti2Renderer.MountAsync<CandleUiRoot>(hti2Parameters);
            Assert.Single(secondHti2Renderer.Components<FluentAutocomplete<PatientR4, string>>());
            secondPage = Assert.Single(secondHti2Renderer.Components<UsCoreHti2ContentsR4>());
            Assert.NotSame(firstPage, secondPage);
        }
        AssertPatientEventsAfterDisposal(2, firstPage, secondPage);

        Delegate[] GetPatientEventHandlers(string eventName)
        {
            Type publisherType = patientStore.GetType();
            FieldInfo field = publisherType.GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"Missing {eventName} backing field on {publisherType}.");
            Assert.Equal(typeof(EventHandler<StoreInstanceEventArgs>), field.FieldType);
            object? value = field.GetValue(patientStore);
            return value is null ? [] : Assert.IsType<EventHandler<StoreInstanceEventArgs>>(value).GetInvocationList();
        }

        void AssertPatientEventsAfterDisposal(int cycle, params UsCoreHti2ContentsR4[] disposedPages)
        {
            Delegate[] createdHandlers = GetPatientEventHandlers(createdEvent);
            Delegate[] deletedHandlers = GetPatientEventHandlers(deletedEvent);
            for (int i = 0; i < disposedPages.Length; i++)
            {
                UsCoreHti2ContentsR4 page = disposedPages[i];
                Assert.False(createdHandlers.Any(handler => ReferenceEquals(handler.Target, page)),
                    $"Cycle {cycle} after disposal: Patient {createdEvent} still targets the HTI-2 page from cycle {i + 1}.");
                Assert.False(deletedHandlers.Any(handler => ReferenceEquals(handler.Target, page)),
                    $"Cycle {cycle} after disposal: Patient {deletedEvent} still targets the HTI-2 page from cycle {i + 1}.");
            }
            Assert.Equal<Delegate>(createdBaseline, createdHandlers);
            Assert.Equal<Delegate>(deletedBaseline, deletedHandlers);
        }

        async Task Activate(string id)
        {
            await EventuallyAsync(pickerRenderer, () =>
                pickerRenderer.Components<FluentDataGridRow<InstanceTableRec>>().Any(row => row.Item?.Id == id));
            await pickerRenderer.Dispatcher.InvokeAsync(() => grid.OnRowClick.InvokeAsync(
                pickerRenderer.Components<FluentDataGridRow<InstanceTableRec>>().Single(row => row.Item?.Id == id)));
        }

        async Task HeaderPreservesSelection()
        {
            ResourcePicker picker = Assert.Single(pickerRenderer.Components<ResourcePicker>());
            string[] selected = picker.SelectedIds.Order().ToArray();
            int callbacks = selections.Count;
            FluentDataGridRow<InstanceTableRec> header = pickerRenderer.Components<FluentDataGridRow<InstanceTableRec>>()
                .Single(row => row.RowType == DataGridRowType.Header);
            Assert.Null(header.Item);
            // The real sortable button click bubbles through the real header row's onclick.
            await pickerRenderer.ClickAsync(header, "fluent-button", "title", "Id");
            string[] firstOrder = pickerRenderer.Components<FluentDataGridRow<InstanceTableRec>>()
                .Where(row => row.Item is not null).Select(row => row.Item!.Id).ToArray();
            Assert.Equal(selected, picker.SelectedIds.Order().ToArray());
            Assert.Equal(callbacks, selections.Count);
            Assert.NotEmpty(picker.SelectedIds); // The Login consumer's selected-ID availability is retained.
            await pickerRenderer.ClickAsync(header, "fluent-button", "title", "Id");
            string[] secondOrder = pickerRenderer.Components<FluentDataGridRow<InstanceTableRec>>()
                .Where(row => row.Item is not null).Select(row => row.Item!.Id).ToArray();
            Assert.Equal(firstOrder.Reverse(), secondOrder);
            Assert.Equal(selected, picker.SelectedIds.Order().ToArray());
            Assert.Equal(callbacks, selections.Count);
        }
    }

    /// <summary>The actual typed select updates both key family and algorithm, and errors reach MESSAGES_TOP.</summary>
    [Fact]
    public async Task SmartKeyFamilyChangePreservesAlgorithm()
    {
        RecordingJs js = new();
        using ServiceProvider services = Services(js);
        await using ManagedRenderer renderer = new(services);
        await renderer.MountAsync<CandleUiRoot>(new()
        {
            [nameof(CandleUiRoot.ChildContent)] = LayoutContent(builder =>
            {
                builder.OpenComponent<ClientDetailsPage>(0);
                builder.CloseComponent();
            }),
        });
        ClientDetailsPage page = Assert.Single(renderer.Components<ClientDetailsPage>());
        FluentTabs tabs = Assert.Single(renderer.Components<FluentTabs>());
        await renderer.Dispatcher.InvokeAsync(() => tabs.ActiveTabIdChanged.InvokeAsync("manual"));
        FluentSelect<string, string> family = renderer.Components<FluentSelect<string, string>>()
            .Single(select => select.Label == "Key Cryptographic Family (kty)");
        Assert.True(family.ValueChanged.HasDelegate);
        await renderer.Dispatcher.InvokeAsync(() => family.ValueChanged.InvokeAsync("EC"));
        Assert.Equal("EC", page.ClientKey!.Kty);
        Assert.Equal("ES384", page.ClientKey.Alg);
        await renderer.Dispatcher.InvokeAsync(() => family.ValueChanged.InvokeAsync("RSA"));
        Assert.Equal("RSA", page.ClientKey.Kty);
        Assert.Equal("RS384", page.ClientKey.Alg);
        FluentButton save = renderer.Components<FluentButton>().Single(button =>
            renderer.Text(button.ChildContent) == "Save");
        await renderer.Dispatcher.InvokeAsync(() => save.OnClick.InvokeAsync(new MouseEventArgs()));
        await EventuallyAsync(renderer, () => renderer.Components<FluentMessageBar>().Any());
        Assert.Contains("Client Name is required.", renderer.RenderedText());
        Assert.Equal(MessageBarIntent.Error, Assert.Single(renderer.Components<FluentMessageBar>()).Intent);
        Assert.Single(renderer.Components<FluentMessageBarProvider>());
    }

    private static RenderFragment LayoutContent(RenderFragment? body = null) => builder =>
    {
        builder.OpenComponent<MainLayout>(0);
        builder.AddAttribute(1, nameof(MainLayout.Body), body);
        builder.CloseComponent();
    };

    private static async Task<(NavMenu Menu, Task Closed)> OpenDrawerAsync(ManagedRenderer renderer)
    {
        await renderer.MountAsync<CandleUiRoot>(new() { [nameof(CandleUiRoot.ChildContent)] = LayoutContent() });
        FluentButton navigation = renderer.Components<FluentButton>().Single(button => button.Title == "Navigation");
        Task closed = renderer.Dispatcher.InvokeAsync(() => navigation.OnClick.InvokeAsync(new MouseEventArgs()));
        await EventuallyAsync(renderer, () => renderer.Components<NavMenu>().Any());
        return (Assert.Single(renderer.Components<NavMenu>()), closed);
    }

    internal static async Task<Uri> SelectPatientAndApproveAsync(
        Uri loginUri, string patientId, IFhirStoreManager stores, ISmartAuthManager auth)
    {
        using ServiceProvider services = Services(new RecordingJs(), stores, auth);
        NavigationManager navigation = services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo(loginUri.ToString());
        await using (ManagedRenderer login = new(services))
        {
            await login.MountAsync<LoginPage>(PageParameters(loginUri, "/smart/login"));
            FluentStack panel = login.Components<FluentStack>().Single(stack =>
                login.Children<ResourcePicker>(stack).Count() == 1 &&
                login.Children<FluentTextInput>(stack).Any(input => input.Label == "User (Patient)"));
            FluentDataGrid<InstanceTableRec> grid = Assert.Single(login.Children<FluentDataGrid<InstanceTableRec>>(panel));
            await EventuallyAsync(login, () => login.Children<FluentDataGridRow<InstanceTableRec>>(grid)
                .Any(row => row.Item?.Id == patientId));
            await login.Dispatcher.InvokeAsync(() => grid.OnRowClick.InvokeAsync(
                login.Children<FluentDataGridRow<InstanceTableRec>>(grid).Single(row => row.Item?.Id == patientId)));
            FluentButton submit = login.Children<FluentButton>(panel).Single(button => login.Text(button.ChildContent) == "Login");
            Assert.False(submit.Disabled);
            await login.Dispatcher.InvokeAsync(() => submit.OnClick.InvokeAsync(new MouseEventArgs()));
        }

        await using ManagedRenderer consent = new(services);
        await consent.MountAsync<AuthorizationPage>(PageParameters(new Uri(navigation.Uri), "/smart/auth"));
        FluentButton approve = consent.Components<FluentButton>().Single(button =>
            consent.Text(button.ChildContent) == "Approve Selected Scopes");
        await consent.Dispatcher.InvokeAsync(() => approve.OnClick.InvokeAsync(new MouseEventArgs()));
        return new Uri(navigation.Uri);

        static Dictionary<string, object?> PageParameters(Uri uri, string path)
        {
            Assert.Equal(path, uri.AbsolutePath);
            return new()
            {
                [nameof(LoginPage.StoreName)] = QueryHelpers.ParseQuery(uri.Query)["store"].ToString(),
                [nameof(LoginPage.Key)] = QueryHelpers.ParseQuery(uri.Query)["key"].ToString(),
            };
        }
    }

    private static ServiceProvider Services(RecordingJs js, IFhirStoreManager? stores = null, ISmartAuthManager? auth = null)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddHttpContextAccessor();
        services.AddSingleton<IJSRuntime>(js);
        services.AddSingleton<NavigationManager>(new TestNavigation());
        services.AddSingleton(new CandleConfig());
        services.AddSingleton(stores ?? new TestStores());
        services.AddSingleton(auth ?? DispatchProxy.Create<ISmartAuthManager, TestAuth>());
        services.AddScoped<ProtectedLocalStorage>();
        services.AddScoped<CandleThemeState>();
        services.AddFluentUIComponents();
        return services.BuildServiceProvider();
    }

    private static async Task EventuallyAsync(ManagedRenderer renderer, Func<bool> predicate)
    {
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(5));
        while (!await renderer.Dispatcher.InvokeAsync(predicate))
        {
            renderer.ThrowErrors();
            await Task.Delay(20, deadline.Token);
        }
        renderer.ThrowErrors();
    }

    private static string SourcePath(string relative, [CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, relative));

    // Framework render-tree access is deliberately confined to this managed test renderer.
#pragma warning disable BL0006
    private sealed class ManagedRenderer(IServiceProvider services)
        : Renderer(services, services.GetRequiredService<ILoggerFactory>())
    {
        private readonly List<Exception> _errors = [];
        private IComponent? _root;
        private int _rootId;
        private bool _disposed;

        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();

        public Task MountAsync<T>(Dictionary<string, object?> parameters) where T : IComponent =>
            Dispatcher.InvokeAsync(async () =>
            {
                _root = InstantiateComponent(typeof(T));
                _rootId = AssignRootComponentId(_root);
                await RenderRootComponentAsync(_rootId, ParameterView.FromDictionary(parameters));
                ThrowErrors();
            });

        public Task UpdateAsync(Dictionary<string, object?> parameters) => Dispatcher.InvokeAsync(async () =>
        {
            await RenderRootComponentAsync(_rootId, ParameterView.FromDictionary(parameters));
            ThrowErrors();
        });

        public IEnumerable<T> Components<T>() => new[] { _root }.Concat(Frames()
            .Where(frame => frame.FrameType == RenderTreeFrameType.Component).Select(frame => frame.Component))
            .OfType<T>().Distinct();

        public IEnumerable<T> Children<T>(IComponent parent) => Walk(Frames().Single(frame =>
            frame.FrameType == RenderTreeFrameType.Component && ReferenceEquals(frame.Component, parent)).ComponentId)
            .Where(frame => frame.FrameType == RenderTreeFrameType.Component).Select(frame => frame.Component).OfType<T>().Distinct();

        public void AssertFeedbackBeforeLayout()
        {
            ArrayRange<RenderTreeFrame> frames = GetCurrentRenderTreeFrames(_rootId);
            RenderTreeFrame[] rootFrames = frames.Array.Take(frames.Count).ToArray();
            int feedback = Array.FindIndex(rootFrames, frame =>
                frame.FrameType == RenderTreeFrameType.Attribute && frame.AttributeValue as string == "candle-messages-top");
            int content = Array.FindIndex(rootFrames, frame =>
                frame.FrameType == RenderTreeFrameType.Component && frame.ComponentType == typeof(MainLayout));
            Assert.True(feedback > 0 && feedback < content);
        }

        public void AssertIconRendered()
        {
            Assert.Contains(Frames(), frame => frame.FrameType == RenderTreeFrameType.Element && frame.ElementName == "svg");
            Assert.Contains(Frames(), frame => frame.FrameType == RenderTreeFrameType.Markup &&
                frame.MarkupContent.Contains("<path", StringComparison.Ordinal));
        }

        public Task ClickAsync(IComponent component, string name, string? attribute = null, string? value = null) =>
            Dispatcher.InvokeAsync(async () =>
            {
                Element target = Assert.Single(Elements(component), element => element.Name == name &&
                    (attribute is null || Equals(element.Attributes.GetValueOrDefault(attribute), value)));
                // Snapshot the actual rendered ancestry; Renderer dispatches handlers, not DOM bubbling.
                for (Element? element = target; element is not null; element = element.Parent)
                {
                    if (element.ClickHandler != 0)
                    {
                        await DispatchEventAsync(element.ClickHandler, null, new MouseEventArgs());
                        ThrowErrors();
                    }
                    if (Equals(element.Attributes.GetValueOrDefault("__internal_stopPropagation_onclick"), true))
                    {
                        break;
                    }
                }
            });

        public void AssertDefaultAnchor(FluentNavItem item, string href)
        {
            Element anchor = Assert.Single(Elements(item), element => element.Name == "a");
            Assert.Equal(href, anchor.Attributes["href"]);
            Assert.NotEqual(0UL, anchor.ClickHandler);
            Assert.NotEqual(true, anchor.Attributes.GetValueOrDefault("__internal_preventDefault_onclick"));
        }

        public void AssertLinkIcon(FluentNavItem item, string href)
        {
            Element anchor = Assert.Single(Elements(item), element =>
                element.Name == "a" && Equals(element.Attributes.GetValueOrDefault("href"), href));
            Assert.Contains(anchor.Descendants(), element => element.Name == "svg");
            Assert.Contains(anchor.Descendants(), element => element.Text.Contains("<path", StringComparison.Ordinal));
        }

        public void AssertDrawerPresentation(NavMenu menu)
        {
            Assert.Single(Components<FluentDialogBody>());
            Element body = Assert.Single(Elements(menu), element => element.Name == "fluent-drawer-body");
            Element title = Assert.Single(body.Descendants(), element =>
                Equals(element.Attributes.GetValueOrDefault("slot"), "title"));
            Assert.Equal("FHIR Candle", string.Concat(title.Descendants().Prepend(title).Select(element => element.Text)));
            Element footer = Assert.Single(body.Descendants(), element =>
                Equals(element.Attributes.GetValueOrDefault("slot"), "footer"));
            Assert.Contains(menu.GetType().Assembly.GetName().Version!.ToString(),
                string.Concat(footer.Descendants().Prepend(footer).Select(element => element.Text)));
            Assert.DoesNotContain(footer.Descendants(), element => element.Name == "button");
        }

        private IEnumerable<Element> Elements(IComponent component)
        {
            int id = ReferenceEquals(component, _root) ? _rootId : Frames()
                .Single(frame => frame.FrameType == RenderTreeFrameType.Component && ReferenceEquals(frame.Component, component))
                .ComponentId;
            Element root = new(string.Empty, null);
            ReadComponent(id, root);
            return root.Descendants();
        }

        private void ReadComponent(int id, Element parent)
        {
            ArrayRange<RenderTreeFrame> frames = GetCurrentRenderTreeFrames(id);
            ReadFrames(frames.Array, 0, frames.Count, parent);
        }

        private void ReadFrames(RenderTreeFrame[] frames, int start, int end, Element parent)
        {
            for (int index = start; index < end; index++)
            {
                RenderTreeFrame frame = frames[index];
                switch (frame.FrameType)
                {
                    case RenderTreeFrameType.Element:
                        Element element = new(frame.ElementName, parent);
                        parent.Children.Add(element);
                        ReadFrames(frames, index + 1, index + frame.ElementSubtreeLength, element);
                        index += frame.ElementSubtreeLength - 1;
                        break;
                    case RenderTreeFrameType.Component:
                        ReadComponent(frame.ComponentId, parent);
                        index += frame.ComponentSubtreeLength - 1;
                        break;
                    case RenderTreeFrameType.Attribute:
                        parent.Attributes[frame.AttributeName] = frame.AttributeValue;
                        if (frame.AttributeName == "onclick")
                        {
                            parent.ClickHandler = frame.AttributeEventHandlerId;
                        }
                        break;
                    case RenderTreeFrameType.Text:
                        parent.Text += frame.TextContent;
                        break;
                    case RenderTreeFrameType.Markup:
                        parent.Text += frame.MarkupContent;
                        break;
                }
            }
        }

        private sealed class Element(string name, Element? parent)
        {
            public string Name { get; } = name;
            public Element? Parent { get; } = parent;
            public Dictionary<string, object?> Attributes { get; } = [];
            public List<Element> Children { get; } = [];
            public ulong ClickHandler { get; set; }
            public string Text { get; set; } = string.Empty;
            public IEnumerable<Element> Descendants() => Children.SelectMany(child => child.Descendants().Prepend(child));
        }

        public string RenderedText() => string.Concat(Frames().Select(frame => frame.FrameType switch
        {
            RenderTreeFrameType.Text => frame.TextContent,
            RenderTreeFrameType.Markup => frame.MarkupContent,
            _ => string.Empty,
        }));

        private IEnumerable<RenderTreeFrame> Frames() => Walk(_rootId);

        private IEnumerable<RenderTreeFrame> Walk(int id)
        {
            ArrayRange<RenderTreeFrame> frames = GetCurrentRenderTreeFrames(id);
            foreach (RenderTreeFrame frame in frames.Array.Take(frames.Count).ToArray())
            {
                yield return frame;
                if (frame.FrameType == RenderTreeFrameType.Component)
                {
                    foreach (RenderTreeFrame child in Walk(frame.ComponentId))
                    {
                        yield return child;
                    }
                }
            }
        }

        public string Text(RenderFragment? fragment)
        {
            if (fragment is null)
            {
                return string.Empty;
            }
            using RenderTreeBuilder builder = new();
            fragment(builder);
            ArrayRange<RenderTreeFrame> frames = builder.GetFrames();
            return string.Concat(frames.Array.Take(frames.Count).Where(frame => frame.FrameType == RenderTreeFrameType.Text)
                .Select(frame => frame.TextContent));
        }

        public void ThrowErrors()
        {
            if (_errors.Count != 0)
            {
                throw new AggregateException(_errors);
            }
        }

        protected override void HandleException(Exception exception) => _errors.Add(exception);

        protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;

        public new async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;
                await base.DisposeAsync();
                ThrowErrors();
            }
        }
    }

#pragma warning restore BL0006

    private sealed record JsCall(string Name, object?[] Args);

    private sealed class RecordingJs : IJSRuntime, IJSObjectReference
    {
        public ConcurrentQueue<JsCall> Calls { get; } = new();
        public bool DelaySnapshot { get; init; }
        public CandleUiRoot.BrowserSnapshot InitialSnapshot { get; init; } = new() { Id = 7 };
        public TaskCompletionSource? PersistGate { get; set; }
        public TaskCompletionSource<CandleUiRoot.BrowserSnapshot> Snapshot { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        // These are synthetic IJSRuntime entry points, not calls into a browser.
#pragma warning disable BL0016
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            RecordAsync<TValue>(identifier, args);
#pragma warning restore BL0016

        private async ValueTask<TValue> RecordAsync<TValue>(string identifier, object?[]? args)
        {
            Calls.Enqueue(new(identifier, args ?? []));
            if (identifier == "candleTheme.connect")
            {
                CandleUiRoot.BrowserSnapshot snapshot = DelaySnapshot
                    ? await Snapshot.Task.WaitAsync(TimeSpan.FromSeconds(15)) : InitialSnapshot;
                return (TValue)(object)snapshot;
            }
            if (identifier == "candleTheme.persist" && PersistGate is not null)
            {
                await PersistGate.Task.WaitAsync(TimeSpan.FromSeconds(15));
            }
            if (identifier == "localStorage.getItem")
            {
                return default!;
            }
            if (typeof(TValue) == typeof(IJSObjectReference))
            {
                return (TValue)(object)this;
            }
            if (typeof(TValue) == typeof(string))
            {
                return (TValue)(object)string.Empty;
            }
            return default!;
        }

#pragma warning disable BL0016
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            RecordAsync<TValue>(identifier, args);
#pragma warning restore BL0016

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("http://localhost/", "http://localhost/");
        public int NavigateCalls { get; private set; }
        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            NavigateCalls++;
            Uri = ToAbsoluteUri(uri).ToString();
        }
    }

    private sealed class TestStores : Dictionary<string, IFhirStore>, IFhirStoreManager
    {
        public TestStores() => Add("test", null!);
        public event EventHandler<EventArgs>? OnChanged;
        public IReadOnlyDictionary<string, IQueryable<PackagePageInfo>> AdditionalPagesByTenant { get; } =
            new Dictionary<string, IQueryable<PackagePageInfo>>();
        public void Init() { }
        public void LoadRiContents(string dir) { }
        public Task LoadRequestedPackages(string supplementalRoot, bool loadExamples) => Task.CompletedTask;
        public void StateHasChanged() => OnChanged?.Invoke(this, EventArgs.Empty);
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>Only the SMART manager's presentation-facing reads are used by these components.</summary>
    public class TestAuth : DispatchProxy
    {
        private readonly Dictionary<string, ClientInfo> _clients = [];
        /// <inheritdoc />
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
        {
            "get_IsEnabled" => true,
            "get_SmartClients" => _clients,
            _ => throw new NotSupportedException(targetMethod?.Name),
        };
    }
}
