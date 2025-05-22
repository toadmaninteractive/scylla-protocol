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
    public class EctoGenerator : IElixirGenerator
    {
        public static readonly StringAttributeDescriptor ContextModuleAttribute = new StringAttributeDescriptor("context.module", IgorAttributeTargets.Module);
        //
        public static readonly StringAttributeDescriptor EctoEntityAttribute = new StringAttributeDescriptor("ecto.entity", IgorAttributeTargets.Record);
        public static readonly StringAttributeDescriptor EctoTableAttribute = new StringAttributeDescriptor("ecto.table", IgorAttributeTargets.Record);
        public static readonly StringAttributeDescriptor EctoTypeAttribute = new StringAttributeDescriptor("ecto.type", IgorAttributeTargets.RecordField);
        public static readonly StringAttributeDescriptor EctoHintAttribute = new StringAttributeDescriptor("ecto.hint", IgorAttributeTargets.RecordField);
        public static readonly StringAttributeDescriptor EctoRelationAttribute = new StringAttributeDescriptor("ecto.relation", IgorAttributeTargets.RecordField);
        public static readonly BoolAttributeDescriptor EctoIndexAttribute = new BoolAttributeDescriptor("ecto.index", IgorAttributeTargets.RecordField);
        public static readonly BoolAttributeDescriptor EctoUniqueAttribute = new BoolAttributeDescriptor("ecto.unique", IgorAttributeTargets.RecordField);
        public static readonly StringAttributeDescriptor EctoCustomIndexAttribute = new StringAttributeDescriptor("ecto.custom_index", IgorAttributeTargets.RecordField);
        public static readonly StringAttributeDescriptor EctoCheckAttribute = new StringAttributeDescriptor("ecto.check", IgorAttributeTargets.RecordField);
        public static readonly BoolAttributeDescriptor EctoProtectedAttribute = new BoolAttributeDescriptor("ecto.protected", IgorAttributeTargets.RecordField);
        //
        public static readonly StringAttributeDescriptor EctoViewAttribute = new StringAttributeDescriptor("ecto.view", IgorAttributeTargets.Record);
        public static readonly StringAttributeDescriptor EctoPreloadAttribute = new StringAttributeDescriptor("ecto.preload", IgorAttributeTargets.Record);
        public static readonly StringAttributeDescriptor EctoTakeAttribute = new StringAttributeDescriptor("ecto.take", IgorAttributeTargets.Record);

        private class EntityField
        {
            public string Relation { get; set; }
            public string Name { get; set; }
            public string Type { get; set; }
            public string Options { get; set; }
            public string Hint { get; set; }
            public bool HasDefault { get; set; }
            public string Default { get; set; }
            public bool IsRequired { get; set; }
            public bool IsProtected { get; set; }
            public string Annotation { get; set; }
        }

        private class MigrationField
        {
            public string Relation { get; set; }
            public string Name { get; set; }
            public string Type { get; set; }
            public string Options { get; set; }
            public string Hint { get; set; }
            public bool HasDefault { get; set; }
            public string Default { get; set; }
            public bool IsRequired { get; set; }
            public bool IsProtected { get; set; }
            public string Annotation { get; set; }
        }

        private List<Form> ecto_types = new List<Form>();

        public void Generate(ExModel model, Module mod)
        {
            // var module = model.ModuleOf(mod);

            // generate ecto types mentioned in ecto entities
            foreach (var form in mod.Enums)
            {
              var ex = model.ModuleOf(form.Module).Module(form.exName);
              // ex.Block($"use Repo.Types.Type, type: {JsonSerialization.JsonTag(form, null)}, values: {form.Fields.JoinStrings(", ", field => $"{field.exName}").Quoted("[", "]")}");
              ex.Block($@"
@doc false
def values(), do: {form.Fields.JoinStrings(", ", field => $"{field.exName}").Quoted("[", "]")}
use Ecto.Type
@impl Ecto.Type
def type, do: :string
@impl Ecto.Type
def cast(x) when {form.exGuardName}(x), do: {{:ok, x}}
def cast(x) do
  {{:ok, from_json!(x)}}
rescue
  _ -> {{:error, type: {JsonSerialization.JsonTag(form, null)}, values: values()}}
end
@impl Ecto.Type
def load(x), do: cast(x)
@impl Ecto.Type
def dump(x), do: {{:ok, to_json!(x)}}
");
            }

            // foreach (var form in mod.Structs.Where(x => x.exEnabled && !x.IsGeneric))
            // foreach (var form in mod.Records.Where(x => x.exEnabled && !x.IsGeneric && !x.IsPatch))
            foreach (RecordForm form in mod.Records)
            {
// Console.WriteLine($"type?: {form.Name} {form.IsGeneric}");
              if (!form.exEnabled || form.IsPatch) continue;
// Console.WriteLine($"type!: {form.Name}");
              var ex = model.ModuleOf(form.Module).Module(form.exName);
              // ex.Attribute("@derive Jason.Encoder");
              if (form.IsGeneric) {
                ex.Block($@"
use Repo.Types.Generic");
              } else {
                string customConverter = (form.Attribute(ExAttributes.JsonCustom) != null) ? $"{form.Attribute(ExAttributes.JsonCustom)}." : "";
                ex.Block($@"
use Ecto.Type
@impl Ecto.Type
def type, do: :map
@impl Ecto.Type
def cast(x) when is_struct(x, __MODULE__), do: {{:ok, x}}
def cast(x) do
  {{:ok, {customConverter}from_json!(x)}}
rescue
  _ -> {{:error, type: {JsonSerialization.JsonTag(form, null)}}}
end
@impl Ecto.Type
def load(x), do: cast(x)
@impl Ecto.Type
def dump(x), do: {{:ok, {customConverter}to_json!(x)}}
");
              }
            }

            // generate ecto entities
            foreach (var record in mod.Records.Where(r => !string.IsNullOrEmpty(r.Attribute(EctoEntityAttribute))))
            {
Console.WriteLine($"entity: {record.Name}");
                string ectoEntity = record.Attribute(EctoEntityAttribute);
                // var ex_ecto = model.File("repo.ex").Module(ectoEntity);
                var ex_ecto = model.File($"repo.{record.exVarName}.ex").Module(ectoEntity);
                ex_ecto.Annotation = record.Annotation ?? $"TODO: FIXME: annotate {record.Name}";
                var ex_sql = model.File("repo_sql.ex").Module("Repo.Sql");
                // ex_sql.Annotation = record.Annotation ?? $"TODO: FIXME: annotate {record.Name}";
                GenerateEntity(record, ex_ecto, ex_sql);
            }

//             // generate ecto types mentioned in ecto entities
//             foreach (var record in ecto_types.Distinct())
//             {
//               // var ex = model.File("repo_types.ex").Module($"Repo.Types.{record.exName}");
//               // GenerateEctoType(record, ex);
//               var ex = model.ModuleOf(record.Module).Module(record.exName);
//               // ex.Use("Ecto.Type\n    @derive Jason.Encoder");
//               ex.Use($@"Ecto.Type
// @impl true
// def type, do: :map
// @impl true
// def load(%__MODULE__{{}} = x), do: {{:ok, x}}
// def load(x), do: {{:ok, from_json!(x)}}
// @impl true
// def dump(x), do: {{:ok, to_json!(x)}}
// @impl true
// def cast(%__MODULE__{{}} = x), do: {{:ok, x}}
// def cast(x), do: {{:ok, from_json!(x)}}
// ");
//             }

/*
            // generate views
            foreach (var record in mod.Records.Where(r => !string.IsNullOrEmpty(r.Attribute(EctoViewAttribute))))
            {
                var ex = model.File("repo_views.ex").Module($"Repo.Views");
                // var ex = model.File("repo_impl.ex").Module(contextModuleName);
                // var contextModuleName = mod.Attribute(ContextModuleAttribute, mod.Name);
                // var ex = model.File(contextModuleName.Format(Notation.LowerUnderscore) + ".ex").Module(contextModuleName);
                ex.Block($@"
# ----------------------------------------------------------------------------
# {record.Annotation ?? $"TODO: FIXME: annotate {record.Name}"}
# ----------------------------------------------------------------------------
");
                GenerateView(record, ex);
            }
*/

        }

        public void GenerateEntity(RecordForm record, ExModule ex_ecto, ExModule ex_sql)
        {
            List<EnumForm> sql_types = new List<EnumForm>();

// TODO: maybe generate migration by elixir means from generated ecto schema?
// TODO: constraints from custom type fields

            string table_name = record.Attribute(EctoTableAttribute, record.exVarName);
            string custom_primary_key = null;
            var entity_fields = new List<EntityField>();
            var entity_indexes = new List<string>();
            var entity_constraints = new List<string>();
            var entity_foreign_constraints = new List<string>();
            var sql_fields = new List<MigrationField>();
            var sql_indexes = new List<string>();
            var sql_constraints = new List<string>();
            string created_at = "false";
            string updated_at = "false";
            foreach (var field in record.Fields)
            {
                // NB: skip ephemeral tag field
                if (field == record.TagField) continue;
                string field_name = field.exAtomName;
                string relation = field.Attribute(EctoRelationAttribute);
                var field_type_and_opts = EctoSchemaFieldDef(field.Type);
                var field_type = field.Attribute(EctoTypeAttribute, field_type_and_opts.Item1);
                var field_opts = field_type_and_opts.Item2;
                // skip vanilla id field, if any specified of integer type
                if (field_name == ":id") {
                  if (field_type == ":integer") continue;
                  custom_primary_key = field_name;
                  entity_indexes.Add($@"unique_constraint({field_name}, name: :{table_name}_pkey, message: ""id_already_exists"")");
                }
                // honor indexes
                if (field.Attribute(EctoCustomIndexAttribute) != null) {
                    // ecto
                    if (field.Attribute(EctoCustomIndexAttribute).Contains(", unique: ")) {
                        string index_name = field.Attribute(EctoCustomIndexAttribute);
                        entity_indexes.Add($@"unique_constraint({field.exAtomName}, name: :{index_name}, message: ""{field.exName}_already_exists"")");
                    }
                    // sql
                    sql_indexes.Add($"create index(:{table_name}, {field.Attribute(EctoCustomIndexAttribute)})");
                }
                if (field.Attribute(EctoIndexAttribute, false)) {
                    // sql
                    sql_indexes.Add($"create index(:{table_name}, [{field.exAtomName}])");
                }
                if (field.Attribute(EctoUniqueAttribute, false)) {
                    // ecto
                    entity_indexes.Add($@"unique_constraint({field.exAtomName}, name: :{table_name}_{field.exName}_index, message: ""{field.exName}_already_exists"")");
                    // sql
                    sql_indexes.Add($"create index(:{table_name}, [{field.exAtomName}], unique: true)");
                }
                // honor constraints
                if (!string.IsNullOrEmpty(field.Attribute(EctoCheckAttribute))) {
                    string attr = field.Attribute(EctoCheckAttribute);
                    string tag = attr.Split(new string[] { ", check: " }, StringSplitOptions.None)[0];
                    string check = attr.Split(new string[] { ", check: " }, StringSplitOptions.None)[1].Replace("@", field.exName);
                    // sql
                    string def = $"create constraint(:{table_name}, :{table_name}_{field.exName}_{tag}, check: {check})";
                    sql_constraints.Add(def);
                    // ecto
                    entity_constraints.Add($@"check_constraint({field.exAtomName}, name: :{table_name}_{field.exName}_{tag}, message: ""invalid_{field.exName}"")");
                }
                // honor vanilla created_at/updated_at fields
                if (field_name == ":created_at") {
                    created_at = field_name;
                    continue;
                }
                if (field_name == ":updated_at") {
                    updated_at = field_name;
                    continue;
                }
                var field_default = field.exDefault;
                var field_required = !field.IsOptional;
                entity_fields.Add(new EntityField {
                    Relation = relation,
                    Name = field_name,
                    Type = field_type,
                    Options = field_opts,
                    Hint = field.Attribute(EctoHintAttribute),
                    HasDefault = field.HasDefault,
                    Default = field_default,
                    IsRequired = field_required,
                    IsProtected = field.Attribute(EctoProtectedAttribute, false),
                    Annotation = field.Annotation,
                });
                field_name = field.exAtomName;
                field_type = EctoMigrationFieldDef(field.Type, ref sql_types);
                if (!string.IsNullOrEmpty(relation)) {
                    if (relation.StartsWith("belongs_to")) {
                        string field_name_without_id = field.exVarName.Substring(0, field.exVarName.Length - 3);
                        if (field_required) {
                            field_type = $"references(:{field_name_without_id}, on_delete: :delete_all)";
                        } else {
                            field_type = $"references(:{field_name_without_id}, on_delete: :nilify_all)";
                        }
                        // add ecto foreign constraint
                        entity_constraints.Add($@"foreign_key_constraint({field.exAtomName}, name: :{table_name}_{field.exName}_fkey, message: ""{field_name_without_id}_not_exists"")");
                    }
                }
                if (field.HasDefault && sql_types.Any(x => x == field.Type)) {
                  // field_default = $"'{field.DefaultValue?.GetType()}'";//vs.Value;
                  field_default = field_default.Substring(1).Quoted("\"");
                }
                if (!(relation ?? "").StartsWith("has_many")) {
                    sql_fields.Add(new MigrationField {
                        Relation = relation,
                        Name = field_name,
                        Type = field_type,
                        HasDefault = field.HasDefault,
                        Default = field_default,
                        IsRequired = field_required,
                        IsProtected = field.Attribute(EctoProtectedAttribute, false),
                        Annotation = field.Annotation,
                    });
                }
            }

            var ecto_schema = entity_fields.Select(field => {
              string def = "  " + (field.Relation ?? $"field {field.Name}, {field.Type}");
              if (field.HasDefault) {
                def += $", default: {field.Default}";
              } else if (field.IsRequired) {
                // def += $", null: false";
              }
              if (!string.IsNullOrEmpty(field.Hint)) {
                def += $", {field.Hint}";
              }
              if (!string.IsNullOrEmpty(field.Options)) {
                def += $", {field.Options}";
              }
              if (field.Name == custom_primary_key) {
                def += ", primary_key: true";
              }
              if (!string.IsNullOrEmpty(field.Annotation)) {
                // def = $"  # {field.Annotation}\n{def}";
                def = $"{def}\t# {field.Annotation}";
              }
              return def;
            });
            ecto_schema = ecto_schema.Append($"  timestamps(inserted_at: {created_at}, updated_at: {updated_at})");

            ex_ecto.Use("Ecto.Schema");
            ex_ecto.Use("Repo.Entity");

            ex_ecto.Block($@"
@all_fields [{entity_fields.JoinStrings(", ", f => f.Name)}]
@protected_fields [{entity_fields.Where(f => f.IsProtected).JoinStrings(", ", f => f.Name)}]
@required_fields [{entity_fields.Where(f => f.IsRequired && !f.IsProtected).JoinStrings(", ", f => f.Name)}]
");

if (custom_primary_key != null) {
  ex_ecto.Block($"@primary_key false");
}
            // ex_ecto.Block("@derive {Jason.Encoder, only: @all_fields}");

            ex_ecto.Block($@"schema ""{table_name}"" do
{ecto_schema.JoinStrings("\n")}
end
");

            ex_ecto.Function($@"
@impl Repo.Entity
def insert_changeset(struct) when is_struct(struct), do: insert_changeset(Map.from_struct(struct))
def insert_changeset(attrs) do
  import Ecto.Changeset
  %__MODULE__{{}}
    |> cast(attrs, @all_fields -- @protected_fields, empty_values: [nil, """"])
    # |> validate_required(@required_fields)
    |> require_presence(@required_fields -- @protected_fields)
{entity_constraints.Select(s => $"    |> {s}").JoinStrings("\n")}
{entity_foreign_constraints.Select(s => $"    |> {s}").JoinStrings("\n")}
{entity_indexes.Select(s => $"    |> {s}").JoinStrings("\n")}
end
");

            ex_ecto.Function($@"
@impl Repo.Entity
def update_changeset(orig, attrs) do
  import Ecto.Changeset
  orig
    |> cast(attrs, @all_fields -- @protected_fields, empty_values: [])
    # |> validate_required(@required_fields)
    |> require_presence(@required_fields -- @protected_fields)
{entity_constraints.Select(s => $"    |> {s}").JoinStrings("\n")}
{entity_foreign_constraints.Select(s => $"    |> {s}").JoinStrings("\n")}
{entity_indexes.Select(s => $"    |> {s}").JoinStrings("\n")}
end
");

            var sql_schema = sql_fields.Select(field => {
              string def = $"    add {field.Name}, {field.Type}";
              if (field.HasDefault) {
                def += $", default: {field.Default}";
              }
              if (field.IsRequired) {
                def += $", null: false";
              }
              if (!string.IsNullOrEmpty(field.Hint)) {
                def += $", {field.Hint}";
              }
              // if (!string.IsNullOrEmpty(field.Options)) {
              //   def += $", {field.Options}";
              // }
              if (field.Name == custom_primary_key) {
                def += ", primary_key: true";
              }
              if (!string.IsNullOrEmpty(field.Annotation)) {
                // def = $"  # {field.Annotation}\n{def}";
                def = $"{def}\t# {field.Annotation}";
              }
              return def;
            });
            sql_schema = sql_schema.Append($"    timestamps(inserted_at: {created_at}, updated_at: {updated_at})");

            // generate sql types mentioned in ecto enum entities
            var sql_statements = sql_types.Select(ef => {
              string sql_type = $"{ef.exName.Format(Notation.LowerUnderscore)}_t";
              // TODO: not more than once!
              return $@"  # {ef.exName}
  execute(
    ""CREATE TYPE {sql_type} AS ENUM ({ef.Fields.JoinStrings(", ", f => $"'{f.Name}'")})"",
    ""DROP TYPE IF EXISTS {sql_type}""
  )";
            });

            ex_sql.Use("Ecto.Migration");

            ex_sql.Block($@"
# ----------------------------------------------------------------------------
# {record.Annotation ?? $"TODO: FIXME: annotate {record.Name}"}
# ----------------------------------------------------------------------------

def define(:{table_name}) do
{sql_statements.Select(s => $"{s}").JoinStrings("\n")}
  create table(:{table_name}{(custom_primary_key != null ? ", primary_key: false" : "")}) do
{sql_schema.JoinStrings("\n")}
  end
{sql_indexes.Select(s => $"  {s}").JoinStrings("\n")}
{sql_constraints.Select(s => $"  {s}").JoinStrings("\n")}
end
");
        }

        public void GenerateEctoType(RecordForm record, ExModule ex)
        {
            var entity_fields = new List<EntityField>();
            foreach (var field in record.Fields)
            {
                var field_type_and_opts = EctoSchemaFieldDef(field.Type);
                var field_type = field_type_and_opts.Item1;
                var field_opts = field_type_and_opts.Item2;
                var field_default = field.exDefault;
                entity_fields.Add(new EntityField {
                    Name = field.exAtomName,
                    Type = field_type,
                    Options = field_opts,
                    HasDefault = field.HasDefault,
                    Default = field_default,
                    IsRequired = !field.IsOptional,
                    Annotation = field.Annotation,
                });
            }

            var ecto_schema = entity_fields.Select(field => {
              string def = $"  field {field.Name}, {field.Type}";
              if (field.HasDefault) {
                def += $", default: {field.Default}";
              } else if (field.IsRequired) {
                // def += $", null: false";
              }
              if (!string.IsNullOrEmpty(field.Options)) {
                def += $", {field.Options}";
              }
              if (!string.IsNullOrEmpty(field.Annotation)) {
                // def = $"  # {field.Annotation}\n{def}";
                def = $"{def}\t# {field.Annotation}";
              }
              return def;
            });

            // ex.Use("Repo.Schema");
            ex.Use("Ecto.Schema");
            ex.Use("Ecto.Type");

            ex.Block($@"
@all_fields [{entity_fields.JoinStrings(", ", f => f.Name)}]
@required_fields [{entity_fields.Where(f => f.IsRequired).JoinStrings(", ", f => f.Name)}]
");

            ex.Block("@derive {Jason.Encoder, only: @all_fields}");

            ex.Block($@"
@primary_key false
embedded_schema do
{ecto_schema.JoinStrings("\n")}
end

@impl true
def type, do: :map

@impl true
def load(%__MODULE__{{}} = x), do: {{:ok, x}}
def load(x), do: cast(x)

@impl true
def dump(x), do: {{:ok, x}}

@impl true
def cast(%__MODULE__{{}} = x), do: {{:ok, x}}
def cast(x) when is_struct(x), do: cast(Map.from_struct(x))
def cast(x) do
  import Ecto.Changeset
  %__MODULE__{{}}
    |> cast(x, @all_fields, empty_values: [])
    |> validate_required(@required_fields)
    |> apply_action(:cast)
end
").Annotation = record.Annotation;
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
@spec list_{record.exVarName}s(Keyword.t() | Ecto.Query.t()) :: [{record.Module.Name}.{record.Name}.t()]
def list_{record.exVarName}s(criteria_or_query \\ []) do
  criteria_or_query
    |> {ectoEntity}.all()
    |> to_{record.exVarName}!()
end

@spec count_{record.exVarName}s(Keyword.t() | Ecto.Query.t()) :: Integer.t()
def count_{record.exVarName}s(criteria_or_query \\ []) do
  criteria_or_query
    |> {ectoEntity}.count()
end

@spec get_collection_of_{record.exVarName}s(Keyword.t() | Ecto.Query.t()) :: DataProtocol.Collection.t({record.Module.Name}.{record.Name}.t())
def get_collection_of_{record.exVarName}s(criteria_or_query \\ []) do
  items = list_{record.exVarName}s(criteria_or_query)
  %DataProtocol.Collection{{items: items}}
end

@spec get_collection_slice_of_{record.exVarName}s(Keyword.t() | Ecto.Query.t()) :: DataProtocol.CollectionSlice.t({record.Module.Name}.{record.Name}.t())
def get_collection_slice_of_{record.exVarName}s(criteria_or_query \\ []) do
  items = list_{record.exVarName}s(criteria_or_query)
  total = count_{record.exVarName}s(criteria_or_query |> Keyword.drop([:offset, :limit, :order_by, :order_dir]))
  %DataProtocol.CollectionSlice{{items: items, total: total}}
end

@spec get_{record.exVarName}!(Keyword.t() | Ecto.Query.t()) :: {record.Module.Name}.{record.Name}.t() | no_return
def get_{record.exVarName}!(criteria_or_query) do
  criteria_or_query
    |> {ectoEntity}.one!()
    |> to_{record.exVarName}!()
end

@spec to_{record.exVarName}!([{ectoEntity}.t()]) :: [{record.Module.Name}.{record.Name}.t()]
def to_{record.exVarName}!([]), do: []
def to_{record.exVarName}!([%{ectoEntity}{{}} | _] = list) do
  list
    |> Repo.preload([{ectoPreloadRules}])
    |> Enum.map(&to_{record.exVarName}!/1)
end

@spec to_{record.exVarName}!({ectoEntity}.t()) :: {record.Module.Name}.{record.Name}.t()
def to_{record.exVarName}!(%{ectoEntity}{{}} = rec) do
  rec = rec |> Repo.preload([{ectoPreloadRules}])
  %{record.Module.Name}.{record.Name}{{
{fields.JoinStrings("\n")}
  }}
end");
        }

        private (string, string) EctoSchemaFieldDef(IType type)
        {
            // if (type is BuiltInType.Atom) return ("Repo.Types.Atom", "");
            if (type is BuiltInType.Atom) return (":string", "");
            if (type is BuiltInType.Binary) return (":binary", "");
            if (type is BuiltInType.Bool) return (":boolean", "");
            if (type is BuiltInType.Dict dict) {
                (var inner_type, var opts) = EctoSchemaFieldDef(dict.ValueType);
                return ($"{{:map, {inner_type}}}", opts);
            }
            if (type is BuiltInType.Flags flags) return EctoSchemaFieldDef(flags.ItemType);
            if (type is BuiltInType.Float) return (":float", "");
            if (type is BuiltInType.Integer integer) {
                var subtype = (new SerializationTags.Primitive(Primitive.FromInteger(integer.Type))).ToString();
                if (subtype == ":int" || subtype == ":long") return (":integer", "");
                return ("Repo.Types.Integer", $"type: {subtype}");
            }
            if (type is BuiltInType.Json) return (":map", "");
            if (type is BuiltInType.List list) {
                (var inner_type, var opts) = EctoSchemaFieldDef(list.ItemType);
                return ($"{{:array, {inner_type}}}", opts);
            }
            // if (type is BuiltInType.OneOf) return ":string";
            if (type is BuiltInType.Optional opt) return EctoSchemaFieldDef(opt.ItemType);
            if (type is BuiltInType.String) return (":string", "");
            if (type is DefineForm df) return EctoSchemaFieldDef(df.Type);
            // if (type is EnumForm ef) return ("Ecto.Enum", $"values: [{ef.Fields.JoinStrings(", ", f => f.exName)}]");
            if (type is EnumForm ef) return ($"{ef.Module.exName}.{ef.exName}", "");
            if (type is StructForm rf) {
                string entityName = rf.Attribute(EctoEntityAttribute);
                if (!string.IsNullOrEmpty(entityName)) return (entityName, "");
                // ecto_types.Add(rf);
                // return ($"Repo.Types.{rf.exName}", "");
                return ($"{rf.Module.exName}.{rf.exName}", "");
            }
            // if (type is TypeForm tf) {
            //   // Console.WriteLine(Inspect(tf));
            //   return ($"{type.ToString()}", "");
            // }
            if (type is GenericArgument ga) {
                // result.type = $"{new SerializationTags.Var(ga)}";
                // result.type = $"{ga.exTypeTagVarName}";
                // result.type = "Repo.Types.Any";
                // TODO:!!!
                // result.type = $"___{ga.Name}___";
                return ($"??????", "");
            }
            if (type is GenericType gt) {
                var proto_type = EctoSchemaFieldDef(gt.Prototype);
                var arg_types = gt.Args.Select(a => EctoSchemaFieldDef(a));
// Console.WriteLine($@"{proto_type.Item1.Replace($"??????", arg_types.JoinStrings(", ", a => a.Item2)}, {proto_type.Item2}");
// Console.WriteLine($@"{arg_types.JoinStrings(", ", a => a.Item2)}");
// Console.WriteLine($"{JsonSerialization.JsonTag(gt.Args[0], null)}");
                // return (proto_type.Item1, $"args: {{{JsonSerialization.JsonTag(gt.Args[0], null)}}}");
                return (proto_type.Item1, "args: " + gt.Args.Select(a => JsonSerialization.JsonTag(a, null).ToString()).JoinStrings(", ").Quoted("{", "}"));

//                 var proto_type = EctoSchemaFieldDef(gt.Prototype);
//                 var arg_types = gt.Args.Select(a => EctoSchemaFieldDef(a));
//                 // result.type = proto_type.type + ".Of." + arg_types.First().type;
// // Console.WriteLine(gt);
// Console.WriteLine(proto_type.Item1);
// Console.WriteLine(proto_type.Item2);
// // Console.WriteLine(arg_types.JoinStrings(", ", a => a.type));
//                 // result.type = ":map";
//                 // result.type = proto_type.type.Replace($"___{(gt.Args[0] as GenericArgument).Name}___", arg_types.JoinStrings(", ", a => a.type));
//                 return (proto_type.Item1.Replace($"______", arg_types.JoinStrings(", ", a => a.Item2)), proto_type.Item2);
           }

            throw new EUnknownType(type.ToString());
        }

        private string EctoMigrationFieldDef(IType type, ref List<EnumForm> sql_types)
        {
            if (type is BuiltInType.Atom) return ":string";
            if (type is BuiltInType.Binary) return ":binary";
            if (type is BuiltInType.Bool) return ":boolean";
            if (type is BuiltInType.Dict) return ":map";
            if (type is BuiltInType.Flags) return ":integer";
            if (type is BuiltInType.Float) return ":float";
            if (type is BuiltInType.Integer) return ":integer";
            if (type is BuiltInType.Json) return ":map";
            if (type is BuiltInType.List list) return $"{{:array, {EctoMigrationFieldDef(list.ItemType, ref sql_types)}}}";
            // if (type is BuiltInType.OneOf) return ":string";
            if (type is BuiltInType.Optional opt) return EctoMigrationFieldDef(opt.ItemType, ref sql_types);
            if (type is BuiltInType.String) return ":text";
            if (type is DefineForm df) return EctoMigrationFieldDef(df.Type, ref sql_types);
            if (type is EnumForm ef) {
                if (!sql_types.Any(x => x == ef)) sql_types.Add(ef);
                return $":{ef.exName.Format(Notation.LowerUnderscore)}_t";
            }
            if (type is RecordForm rf) {
                string entityName = rf.Attribute(EctoEntityAttribute);
                if (!string.IsNullOrEmpty(entityName)) return entityName;
                return ":map";
            }
            // if (type is TypeForm tf) return ":map";
            // if (type is GenericArgument ga) return $"{new SerializationTags.Var(ga)}";
            if (type is GenericType gt) return ":map";
            throw new EUnknownType(type.ToString());
        }

        private string Inspect(Object obj, string indent = "")
        {
            var result = new List<string>();
            var props = obj.GetType().GetProperties();
            foreach (var p in props) {
                result.Add($"{indent}{p.Name}: {p.GetValue(obj, null)}");
            }
            result.Sort(delegate(string x, string y) {
                return string.Compare(x, y, StringComparison.Ordinal);
            });
            return result.JoinStrings("\n");
        }

    }
}
