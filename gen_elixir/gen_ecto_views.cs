using System;
using System.Collections.Generic;
using System.Linq;

using Igor.Elixir.AST;
using Igor.Elixir.Model;
using Igor.Elixir.Render;
using Igor.Text;

namespace Igor.Elixir
{
    [CustomAttributes]
    public class EctoViewGenerator : IElixirGenerator
    {
        public static readonly StringAttributeDescriptor EctoViewAttribute = new StringAttributeDescriptor("ecto.view", IgorAttributeTargets.Record);
        public static readonly StringAttributeDescriptor EctoPreloadAttribute = new StringAttributeDescriptor("ecto.preload", IgorAttributeTargets.Record);
        public static readonly StringAttributeDescriptor EctoTakeAttribute = new StringAttributeDescriptor("ecto.take", IgorAttributeTargets.Record);

        public void Generate(ExModel model, Module mod)
        {
            // generate views
            foreach (var record in mod.Records.Where(r => !string.IsNullOrEmpty(r.Attribute(EctoViewAttribute))))
            {
                var ex = model.File("repo_views.ex").Module($"Repo.Views");
                ex.Block($@"
# ----------------------------------------------------------------------------
# {record.Annotation ?? $"TODO: FIXME: annotate {record.Name}"}
# ----------------------------------------------------------------------------
");
                GenerateView(record, ex);
            }

        }

        public void GenerateView(RecordForm record, ExModule ex)
        {
            string ectoEntity = record.Attribute(EctoViewAttribute);
            string ectoPreload = record.Attribute(EctoPreloadAttribute);

            // collect preload rules
            string ectoPreloadRules = "";
            if (!string.IsNullOrEmpty(ectoPreload)) {
                ectoPreloadRules = string.Join(", ",
                    ectoPreload.Split(' ').Select(x =>
                        x.Contains(".")
                            ? ("{" + string.Join(", ", x.Split('.').Select(y => $":{y}")) + "}")
                            : $":{x}"
                    )
                );
            }

            // collect struct mapper rules
            var fields = record.Fields.Select(field => {
                var accessPath = $"rec.{field.Name}";
                var take = field.Attribute(EctoTakeAttribute);
                if (!string.IsNullOrEmpty(take)) {
                    if (take.StartsWith("&")) {
                        accessPath = $"rec |> then({take})";
                        // accessPath = $"({take}).(rec)";
                    } else if (take.StartsWith("fn")) {
                        accessPath = $"rec |> then({take})";
                        // accessPath = $"({take}).(rec)";
                    } else {
                        string[] steps = take.Split('?');
                        accessPath = string.Join(" && ", steps.Select((step, index) => "rec." + string.Join("", steps.Take(index + 1))));
                    }
                }
                if (field.HasDefault) {
                    accessPath = $"({accessPath}) || {field.Default}";
                }
                string line = $"    {field.Name}: {accessPath},";
                if (!string.IsNullOrEmpty(field.Annotation)) {
                    line += $" # {field.Annotation}";
                }
                return line;
            });

            ex.Block($@"
@spec list_{record.exVarName}s(Keyword.t() | Ecto.Query.t(), Keyword.t()) :: [{record.Module.Name}.{record.Name}.t()]
def list_{record.exVarName}s(criteria_or_query, args \\ []) when is_list(args) do
  criteria_or_query
    |> {ectoEntity}.all()
    |> to_{record.exVarName}!(args)
end

@spec count_{record.exVarName}s(Keyword.t() | Ecto.Query.t()) :: Integer.t()
def count_{record.exVarName}s(criteria_or_query) do
  criteria_or_query
    |> {ectoEntity}.count()
end

@spec get_collection_of_{record.exVarName}s(Keyword.t() | Ecto.Query.t(), Keyword.t()) :: DataProtocol.Collection.t({record.Module.Name}.{record.Name}.t())
def get_collection_of_{record.exVarName}s(criteria_or_query, args \\ []) when is_list(args) do
  items = list_{record.exVarName}s(criteria_or_query, args)
  %DataProtocol.Collection{{items: items}}
end

@spec get_collection_slice_of_{record.exVarName}s(Keyword.t() | Ecto.Query.t(), Keyword.t()) :: DataProtocol.CollectionSlice.t({record.Module.Name}.{record.Name}.t())
def get_collection_slice_of_{record.exVarName}s(criteria_or_query, args \\ []) when is_list(args) do
  items = list_{record.exVarName}s(criteria_or_query, args)
  total = count_{record.exVarName}s(criteria_or_query |> Keyword.drop([:offset, :limit, :order_by, :order_dir]))
  %DataProtocol.CollectionSlice{{items: items, total: total}}
end

@spec get_{record.exVarName}!(Keyword.t() | Ecto.Query.t(), Keyword.t()) :: {record.Module.Name}.{record.Name}.t() | no_return
def get_{record.exVarName}!(criteria_or_query, args \\ []) when is_list(args) do
  criteria_or_query
    |> {ectoEntity}.one!()
    |> to_{record.exVarName}!(args)
end

@spec to_{record.exVarName}!({ectoEntity}.t() | [{ectoEntity}.t()], Keyword.t()) :: {record.Module.Name}.{record.Name}.t() | [{record.Module.Name}.{record.Name}.t()]
def to_{record.exVarName}!(list, args \\ [])
def to_{record.exVarName}!([], args) when is_list(args), do: []
def to_{record.exVarName}!([%{ectoEntity}{{}} | _] = list, args) when is_list(args) do
  list
    |> Repo.preload([{ectoPreloadRules}])
    |> Enum.map(& to_{record.exVarName}!(&1, args))
end
def to_{record.exVarName}!(%{ectoEntity}{{}} = rec, args) when is_list(args) do
  rec = rec |> Repo.preload([{ectoPreloadRules}])
  %{record.Module.Name}.{record.Name}{{
{fields.JoinStrings("\n")}
  }}
end");
        }

    }
}
