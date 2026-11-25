using System.Text.Json;
using System.Text.Json.Nodes;
using MomentumGroves.Models;
using MomentumGroves.Repositories;

namespace MomentumGroves.Service;

public sealed partial class MomentumDataService
{
    public const string TrainingKey = MomentumDataRepository.TrainingKey;
    private const string LegacyTrainingKey = MomentumDataRepository.LegacyTrainingKey;

    private const int MaximumTrainingEdits = 100;
    private const int MaximumExerciseIdLength = 300;

    /// <summary>
    /// Checks the size and contents of a mobile training weight edit batch.
    /// </summary>
    /// <param name="edits">The edits submitted by the mobile client.</param>
    /// <returns>True when all edit IDs, exercise IDs, and weights are valid.</returns>
    public bool IsValidTrainingEditBatch(IReadOnlyList<TrainingWeightEdit>? edits)
    {
        return edits is not null && edits.Count <= MaximumTrainingEdits &&
            edits.All(edit => edit is not null && edit.ChangeId != Guid.Empty &&
                !string.IsNullOrWhiteSpace(edit.ExerciseId) && edit.ExerciseId.Length <= MaximumExerciseIdLength &&
                edit.Kg >= 0 && edit.BaseKg >= 0);
    }

    /// <summary>
    /// Applies mobile weight edits in one transaction when their original weights match the stored values.
    /// </summary>
    /// <param name="edits">The exercise weight changes submitted by the mobile client.</param>
    /// <param name="token">The token used to cancel the operation.</param>
    /// <returns>The resulting programmes and the outcome of each edit.</returns>
    public Task<TrainingSyncResult> SyncTrainingAsync(List<TrainingWeightEdit> edits, CancellationToken token)
    {
        return _repository.WithTrainingAsync<TrainingSyncResult>(edits.Count > 0, (current, legacy) =>
        {
            var programmes = NormalizeTraining(current, legacy);
            var exercises = ExercisesById(programmes);
            var results = new List<TrainingWeightResult>();
            var changed = false;
            foreach (var edit in edits)
            {
                if (!exercises.TryGetValue(edit.ExerciseId,
                    out var exercise) || exercise["type"]?.ToString() == "cardio")
                {
                    results.Add(new(edit.ChangeId, "missing"));
                    continue;
                }

                var kg = Weight(exercise);
                if (kg != edit.BaseKg && kg != edit.Kg)
                {
                    results.Add(new(edit.ChangeId, "conflict"));
                    continue;
                }

                if (kg != edit.Kg)
                {
                    exercise["kg"] = edit.Kg;
                    changed = true;
                }

                results.Add(new(edit.ChangeId, "saved"));
            }

            return (new TrainingSyncResult(programmes, results), changed ? programmes : null);
        }, token);
    }

    /// <summary>
    /// Saves browser edits and detects conflicting changes to stored weights.
    /// </summary>
    /// <param name="save">The programme document and its original exercise weights.</param>
    /// <param name="token">The token used to cancel the operation.</param>
    /// <returns>A task that completes after the training document is saved.</returns>
    public Task SaveTrainingAsync(TrainingSave save, CancellationToken token)
    {
        Validate(TrainingKey, save.Programmes);
        if (JsonNode.Parse(save.Programmes.GetRawText()) is not JsonObject incoming ||
            incoming["groups"] is not JsonArray || save.OriginalWeights is null)
        {
            throw new ArgumentException("Invalid training programmes.");
        }

        incoming = NormalizeTraining(incoming, null);

        return _repository.WithTrainingAsync(true, (currentDocument, legacyDocument) =>
        {
            var current = ExercisesById(NormalizeTraining(currentDocument, legacyDocument));
            var submitted = ExercisesById(incoming);

            if (_repository.UsesCloudStorage && save.OriginalWeights.Any(original =>
                    !submitted.ContainsKey(original.Key) && current.TryGetValue(original.Key, out var exercise) &&
                    Weight(exercise) != original.Value))
            {
                throw new TrainingConflictException();
            }

            foreach (var (id, exercise) in submitted)
            {
                if (!current.TryGetValue(id, out var savedExercise))
                {
                    continue;
                }

                var stored = Weight(savedExercise);
                var requested = Weight(exercise);
                if (!save.OriginalWeights.TryGetValue(id, out var original))
                {
                    if (_repository.UsesCloudStorage && requested != stored)
                    {
                        throw new TrainingConflictException();
                    }
                    continue;
                }

                if (requested == original)
                {
                    exercise["kg"] = stored;
                }
                else if (_repository.UsesCloudStorage && stored != original && stored != requested)
                {
                    throw new TrainingConflictException();
                }
            }

            return (true, incoming);
        }, token);
    }

    /// <summary>
    /// Converts legacy programme groups to the current format and fills in missing exercise IDs.
    /// </summary>
    /// <param name="current">The current training document, if present.</param>
    /// <param name="legacy">The legacy training document, if present.</param>
    /// <returns>A training document containing programme groups and stable exercise IDs.</returns>
    private static JsonObject NormalizeTraining(JsonNode? current, JsonNode? legacy)
    {
        var source = (current ?? legacy) as JsonObject;
        if (source?["groups"] is not JsonArray)
        {
            var groups = new JsonArray();
            foreach (var (id, name) in new[]
                     {
                         ("full", "My programmes"), ("custom", "My other programmes")
                     })
            {
                if (source?[id] is JsonArray { Count: > 0 } programmes)
                {
                    groups.Add(new JsonObject
                    {
                        ["id"] = id, ["name"] = name, ["programmes"] = programmes.DeepClone()
                    });
                }
            }

            source = new JsonObject
            {
                ["groups"] = groups,
                ["dashboardGroupId"] = groups.Count > 0 && groups[0]?["id"]?.ToString() == "full" ? "full" : null
            };
        }

        foreach (var group in ((JsonArray)source["groups"]!).OfType<JsonObject>())
        {
            if (group["programmes"] is not JsonArray programmes)
            {
                continue;
            }

            foreach (var programme in programmes.OfType<JsonObject>())
            {
                if (programme["exercises"] is not JsonArray exercises)
                {
                    continue;
                }

                for (var index = 0; index < exercises.Count; index++)
                {
                    if (exercises[index] is JsonObject exercise
                        && string.IsNullOrWhiteSpace(exercise["id"]?.ToString()))
                    {
                        exercise["id"] = $"legacy:{group["id"]}:{programme["id"]}:{index}";
                    }
                }
            }
        }

        return source;
    }

    /// <summary>
    /// Indexes training exercises that have an ID.
    /// </summary>
    /// <param name="programmes">The normalized training programme document.</param>
    /// <returns>The first exercise found for each ID, keyed by that ID.</returns>
    private static Dictionary<string, JsonObject> ExercisesById(JsonObject programmes)
    {
        var result = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var group in ((JsonArray)programmes["groups"]!).OfType<JsonObject>())
        {
            if (group["programmes"] is not JsonArray groupProgrammes)
            {
                continue;
            }

            foreach (var programme in groupProgrammes.OfType<JsonObject>())
            {
                if (programme["exercises"] is not JsonArray exercises)
                {
                    continue;
                }

                foreach (var exercise in exercises.OfType<JsonObject>())
                {
                    if (exercise["id"]?.ToString() is { Length: > 0 } id)
                    {
                        result.TryAdd(id, exercise);
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Reads an exercise weight using culture-independent numeric parsing.
    /// </summary>
    /// <param name="exercise">The exercise that contains the weight value.</param>
    /// <returns>The weight in kilograms, or zero if the value is absent or invalid.</returns>
    private static decimal Weight(JsonObject exercise)
    {
        return decimal.TryParse(exercise["kg"]?.ToString(), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var kg)
            ? kg
            : 0;
    }
}
