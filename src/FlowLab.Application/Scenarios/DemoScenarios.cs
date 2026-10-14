using FlowLab.Domain;

namespace FlowLab.Application;

public static class DemoScenarios
{
    public static IEnumerable<ScenarioInput> Create()
    {
        yield return new("Платёжный шлюз",
            "Базовый поток: API → внешний платёжный сервис. Исследуйте влияние нагрузки и повторов.", new());

        yield return new("Час пик",
            "Очередь перед медленным сервисом. Найдите узкое место и увеличьте число обработчиков.",
            new ScenarioSettings
            {
                ArrivalRate = 160,
                Nodes =
                [
                    new() { Id = "buffer", Kind = NodeKind.Queue, Name = "Request buffer", QueueCapacity = 150 },
                    new() { Id = "orders", Name = "Order Service", Workers = 2, ProcessingMs = 40 }
                ]
            });

        yield return new("Отказ зависимости",
            "Внешний API недоступен с 20-й по 30-ю секунду. Сравните запуск с circuit breaker.",
            new ScenarioSettings
            {
                Nodes =
                [
                    new() { Id = "api", Name = "API Gateway" },
                    new()
                    {
                        Id = "payment", Name = "Payment API", Kind = NodeKind.External,
                        Workers = 8, ProcessingMs = 50, OutageDurationMs = 10000, OutageFails = true
                    }
                ]
            });
    }
}
