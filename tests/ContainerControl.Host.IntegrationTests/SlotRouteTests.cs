using ContainerControl.Modules.Delivery.Runs;
using ContainerControl.Modules.Edge.Domains;

namespace ContainerControl.Host.IntegrationTests;

public class SlotRouteTests
{
    [Fact]
    public void Director_omits_a_zero_weight_and_keeps_the_live_release()
    {
        var weights = TrafficPlan.Weights("ccapp", TrafficPlan.Classic, TrafficPlan.Blue, 0);
        var labels = SlotRouteLabels.Director("ccapp", "web.apps.example.com", tls: false, "edge");
        var proxy = SlotRouteLabels.ProxyConfig([("ccapp-live", 80, weights[0].Weight)]);

        Assert.Contains("server ccapp-live:80 weight=100;", proxy, StringComparison.Ordinal);
        Assert.DoesNotContain("blue", proxy, StringComparison.Ordinal);
        Assert.DoesNotContain(labels.Keys, key => key.Contains("weighted", StringComparison.Ordinal));
        Assert.Equal("80", labels["traefik.http.services.ccappslotlb.loadbalancer.server.port"]);
        Assert.Equal("Host(`web.apps.example.com`)", labels["traefik.http.routers.ccappslot.rule"]);
        Assert.Equal("web", labels["traefik.http.routers.ccappslot.entrypoints"]);
        Assert.Equal("200", labels["traefik.http.routers.ccappslot.priority"]);
        Assert.False(labels.ContainsKey("traefik.http.routers.ccappslot.tls"));
    }

    [Fact]
    public void Canary_splits_the_weighted_route_and_tls_stays_on_that_router()
    {
        var weights = TrafficPlan.Weights("ccapp", TrafficPlan.Blue, TrafficPlan.Green, 10);
        var labels = SlotRouteLabels.Director("ccapp", "https://Web.apps.example.com/home", tls: true, "edge");
        var proxy = SlotRouteLabels.ProxyConfig(weights.Select(weight => (weight.ServiceName, 80, weight.Weight)).ToArray());

        Assert.Contains("server ccapp-blue:80 weight=90;", proxy, StringComparison.Ordinal);
        Assert.Contains("server ccapp-green:80 weight=10;", proxy, StringComparison.Ordinal);
        Assert.Equal("websecure", labels["traefik.http.routers.ccappslot.entrypoints"]);
        Assert.Equal("true", labels["traefik.http.routers.ccappslot.tls"]);
        Assert.Equal("le", labels["traefik.http.routers.ccappslot.tls.certresolver"]);
    }

    [Fact]
    public void Swap_sends_all_traffic_to_the_new_slot()
    {
        var weights = TrafficPlan.Weights("ccapp", TrafficPlan.Green, candidateSlot: null, candidatePercent: 0);

        Assert.Equal([(TrafficPlan.Green, "ccapp-green", 100)], weights.Select(weight => (weight.Slot, weight.ServiceName, weight.Weight)).ToArray());
    }

    [Fact]
    public void Backend_labels_do_not_publish_a_router_rule()
    {
        var labels = SlotRouteLabels.Backend("ccapp-blue", 8080, "edge");

        Assert.Equal("8080", labels["traefik.http.services.ccapp-blue.loadbalancer.server.port"]);
        Assert.Equal("edge", labels["traefik.docker.network"]);
        Assert.DoesNotContain(labels.Keys, key => key.Contains(".rule", StringComparison.Ordinal));
    }

    [Fact]
    public void The_second_color_is_the_one_that_is_not_live()
    {
        Assert.Equal(TrafficPlan.Blue, TrafficPlan.CandidateFor(TrafficPlan.Classic));
        Assert.Equal(TrafficPlan.Blue, TrafficPlan.CandidateFor(null));
        Assert.Equal(TrafficPlan.Green, TrafficPlan.CandidateFor(TrafficPlan.Blue));
        Assert.Equal(TrafficPlan.Blue, TrafficPlan.CandidateFor(TrafficPlan.Green));
    }
}
