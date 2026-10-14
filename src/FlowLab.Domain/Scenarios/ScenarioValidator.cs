namespace FlowLab.Domain;

public static class ScenarioValidator
{
    public static List<string> Validate(ScenarioInput input)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 120)
        {
            errors.Add("Название должно содержать от 1 до 120 символов.");
        }

        if (input.Description is null || input.Description.Length > 2000)
        {
            errors.Add("Описание: максимум 2000 символов.");
        }

        var settings = input.Settings;
        if (settings is null || settings.Nodes is null || settings.Resilience is null || settings.Nodes.Any(node => node is null))
        {
            errors.Add("Укажите настройки, список узлов и политику устойчивости.");
            return errors;
        }
        if (!double.IsFinite(settings.ArrivalRate) || settings.ArrivalRate <= 0 || settings.ArrivalRate > 10000)
        {
            errors.Add("Поток: от 0 до 10 000 запросов/с (не включая 0).");
        }

        if (!double.IsFinite(settings.DurationMs) || settings.DurationMs < 100 || settings.DurationMs > 3600000)
        {
            errors.Add("Длительность: от 100 до 3 600 000 мс.");
        }

        if (settings.ArrivalRate * settings.DurationMs / 1000 > 1000000)
        {
            errors.Add("Не более 1 000 000 ожидаемых запросов за запуск.");
        }

        if (!Enum.IsDefined(settings.ArrivalDistribution))
        {
            errors.Add("Неизвестная модель потока.");
        }

        if (settings.Nodes.Count is < 1 or > 32 || settings.Nodes.All(node => node.Kind == NodeKind.Queue))
        {
            errors.Add("Добавьте от 1 до 32 узлов и хотя бы один сервис.");
        }

        if (settings.Nodes.Select(node => node.Id).Distinct().Count() != settings.Nodes.Count)
        {
            errors.Add("Идентификаторы узлов должны быть уникальны.");
        }

        foreach (var node in settings.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Id) || node.Id is "source" or "sink" || string.IsNullOrWhiteSpace(node.Name) || node.Name.Length > 80)
            {
                errors.Add("Укажите идентификатор и название узла (до 80 символов).");
            }

            if (!Enum.IsDefined(node.Kind) || !Enum.IsDefined(node.Distribution) || !Enum.IsDefined(node.QueueDiscipline) || !Enum.IsDefined(node.Routing))
            {
                errors.Add("Неизвестный тип узла или распределения.");
            }

            if (node.Workers is < 1 or > 1024 || !IsWithin(node.ProcessingMs, 0.01, 60000) || !IsWithin(node.Variability, 0, 1) || !IsWithin(node.ErrorProbability, 0, 1) || node.QueueCapacity is < 0 or > 1000000)
            {
                errors.Add($"{node.Name}: неверные параметры обработки или очереди.");
            }

            if (!IsWithin(node.OutageStartMs, 0, 3600000) || !IsWithin(node.OutageDurationMs, 0, 3600000) || !IsWithin(node.OutageLatencyMultiplier, 1, 100))
            {
                errors.Add($"{node.Name}: неверные параметры отказа.");
            }
        }
        if (settings.Connections is null && (settings.Nodes.LastOrDefault()?.Kind == NodeKind.Queue || settings.Nodes.Zip(settings.Nodes.Skip(1)).Any(p => p.First.Kind == NodeKind.Queue && p.Second.Kind == NodeKind.Queue)))
        {
            errors.Add("Очередь должна находиться непосредственно перед сервисом.");
        }

        for (var i = 0; settings.Connections is null && i < settings.Nodes.Count; i++)
        {
            var node = settings.Nodes[i];
            if (node.ParallelWithPrevious && (i == 0 || node.Kind == NodeKind.Queue || settings.Nodes[i - 1].Kind == NodeKind.Queue))
            {
                errors.Add("Параллельная ветвь должна следовать за сервисом, а не за очередью или источником.");
            }
        }

        if (settings.Connections is not null && errors.Count == 0)
        {
            errors.AddRange(GraphValidator.Validate(settings));
        }
        else if (settings.Connections is null && settings.Nodes.Any(node => node.Kind == NodeKind.Broker))
        {
            errors.Add("Брокер доступен в режиме графа связей.");
        }

        var resilience = settings.Resilience;
        if (!IsWithin(resilience.TimeoutMs, 1, 120000) || resilience.MaxAttempts is < 1 or > 10 || !IsWithin(resilience.RetryDelayMs, 1, 60000) || !IsWithin(resilience.MaxRetryDelayMs, resilience.RetryDelayMs, 120000) || resilience.FailureThreshold is < 1 or > 1000 || !IsWithin(resilience.BreakDurationMs, 1, 120000))
        {
            errors.Add("Некорректные параметры устойчивости.");
        }

        return errors;
    }
    private static bool IsWithin(double value, double min, double max) => double.IsFinite(value) && value >= min && value <= max;
}
