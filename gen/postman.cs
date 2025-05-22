using System.Collections.Generic;
using System.Linq;
using Igor.Core.AST;
using Igor.Text;
using Json;

using System;

namespace Igor
{
    public class PostmanTarget : ITarget
    {
        public static readonly StringAttributeDescriptor CollectionNameAttribute = new StringAttributeDescriptor("collection_name");

        public string Name
        {
            get { return "postman"; }
        }

        public System.Version DefaultVersion
        {
            get { return new System.Version(); }
        }

        public IReadOnlyCollection<AttributeDescriptor> SupportedAttributes
        {
            get { return new List<AttributeDescriptor>
            {
                CollectionNameAttribute,
            }; }
        }

        public IReadOnlyCollection<TargetFile> Generate(IReadOnlyList<Igor.Declarations.Module> modules, IReadOnlyList<System.Reflection.Assembly> scripts)
        {
            var astModules = AstMapper.Map(modules);
            var webServices = astModules.SelectMany(mod => mod.WebServices);

            var items = new JsonArray();

            foreach (var webService in webServices)
            {
                string baseUrl = "";
                foreach (var attr in webService.Attributes) {
                    if (attr.Target == "elixir" && attr.Name == "http.base_url") {
                        if (attr.Value is AttributeValue.String sv) {
                            try {
                                baseUrl = (new UriBuilder(sv.Value)).Path;
                            } catch {
                            } finally {}
                        }
                    }
                }

                var resourceItems = new JsonArray();
                foreach (var resource in webService.Resources)
                {
                    var headers = new JsonArray();
                    foreach (var header in resource.RequestHeaders)
                    {
                        var h = new JsonObject
                        {
                            { "key", header.Name },
                            { "value", header.IsStatic ? header.StaticValue : header.Var.Name.Quoted("{{", "}}") }
                        };
                        if (header.Var != null && header.Var.Annotation != null)
                            h["description"] = header.Var.Annotation;
                        headers.Add(h);
                    }

                    var path = resource.Path.Select(p => p.IsStatic ? ImmutableJson.Create(p.StaticValue) : ImmutableJson.Create(p.Var.Name.Quoted("{{", "}}"))).Prepend(baseUrl);
                    var query = new JsonArray();
                    foreach (var q in resource.Query)
                    {
                        ImmutableJson qValue;
                        if (q.IsStatic)
                            qValue = q.StaticValue;
                        else if (q.Var.DefaultValue != null)
                            qValue = ValueToJson(q.Var.DefaultValue, q.Var);
                        else
                            qValue = q.Var.Name.Quoted("{{", "}}");
                        var queryItem = new JsonObject {
                            { "key", q.Parameter },
                            { "value", qValue },
                            { "disabled", !q.IsStatic && q.Var.Type is BuiltInType.Optional }
                        };
                        if (!q.IsStatic && q.Var.Annotation != null)
                            queryItem["description"] = q.Var.Annotation;
                        query.Add(queryItem);
                    }

                    var request = new JsonObject
                    {
                        { "url", new JsonObject
                            {
                                { "host", "{{url}}" },
                                { "path", new JsonArray(path) },
                                { "query", query }
                            }
                        },
                        { "method", resource.Method.ToString() },
                        { "header", headers }
                    };

                    if (resource.RequestContent != null)
                    {
                        request["body"] = new JsonObject
                        {
                            { "mode", "raw" },
                            { "raw", GetSampleValue(resource.RequestContent.Type, resource).ToString() }
                        };
                    }
                    var item = new JsonObject
                    {
                        { "name", resource.Name },
                        { "request", request },
                    };
                    if (resource.Annotation != null)
                        item["description"] = resource.Annotation;
                    resourceItems.Add(item);
                }

                var itemGroup = new JsonObject
                {
                    { "name", webService.Name },
                    { "item", resourceItems },
                };
                if (webService.Annotation != null)
                    itemGroup["description"] = webService.Annotation;
                items.Add(itemGroup);
            }

            string collectionName = Context.Instance.Attribute(CollectionNameAttribute, "PostmanCollection");
            var info = new JsonObject {
                { "name", collectionName },
                { "schema", "https://schema.getpostman.com/json/collection/v2.1.0/collection.json" }
            };
            var collection = new JsonObject
            {
                { "info", info },
                { "item", items }
            };
            var serializedCollection = collection.ToString(JsonFormat.Multiline);
            return new TargetFile[] { new TargetFile(collectionName + ".postman_collection.json", serializedCollection, false) };
        }

        private ImmutableJson ValueToJson(Value value, Statement context)
        {
            if (value is Value.String)
                return ((Value.String)value).Value;
            else if (value is Value.Bool)
                return ((Value.Bool)value).Value;
            else if (value is Value.Float)
                return ((Value.Float)value).Value;
            else if (value is Value.Integer)
                return ((Value.Integer)value).Value;
            else if (value is Value.Enum)
                return ((Value.Enum)value).Field.jsonKey;
            else if (value is Value.EmptyObject)
                return ImmutableJson.EmptyObject;
            else if (value is Value.Dict)
                return ImmutableJson.EmptyObject;
            else if (value is Value.List)
                return ImmutableJson.EmptyArray;
            context.Warning("Don't know how to translate value " + value.ToString() + " to json");
            return ImmutableJson.Null;
        }

        private ImmutableJson GetSampleValue(IType type, Value defaultValue, Statement context)
        {
            if (defaultValue == null)
                return GetSampleValue(type, context);
            else
                return ValueToJson(defaultValue, context);
        }

        private ImmutableJson GetSampleValue(IType type, Statement context)
        {
            if (type is BuiltInType.Bool)
                return true;
            else if (type is BuiltInType.Integer)
                return 0;
            else if (type is BuiltInType.Float)
                return 0.0;
            else if (type is BuiltInType.String)
                return "string";
            else if (type is BuiltInType.Binary)
                return "binary";
            else if (type is BuiltInType.Atom)
                return "atom";
            else if (type is BuiltInType.Optional)
                return ImmutableJson.Null;
            else if (type is BuiltInType.Json)
                return ImmutableJson.EmptyObject;
            else if (type is BuiltInType.List)
                return new JsonArray { GetSampleValue(((BuiltInType.List)type).ItemType, context) };
            else if (type is BuiltInType.Dict)
                return new JsonObject { { GetSampleValue(((BuiltInType.Dict)type).KeyType, context).ToString(), GetSampleValue(((BuiltInType.Dict)type).ValueType, context) } };
            else if (type is RecordForm)
            {
                var result = new JsonObject();
                foreach (var f in ((RecordForm)type).Fields)
                {
                    result[f.jsonKey] = GetSampleValue(f.Type, f.DefaultValue, (RecordForm)type);
                }
                return result;
            }
            else if (type is VariantForm)
            {
                var v = (VariantForm)type;
                return GetSampleValue(v.Records.First(), context);
            }
            else if (type is EnumForm)
                return ((EnumForm)type).Fields.First().jsonKey;
            else if (type is DefineForm)
                return GetSampleValue(((DefineForm)type).Type, context);
            context.Warning("Don't know how to provide sample value for type " + type.ToString());
            return ImmutableJson.Null;
        }
    }
}