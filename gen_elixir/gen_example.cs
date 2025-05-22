using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Igor.Elixir.AST;
using Igor.Elixir.Model;
using Igor.Elixir.Render;
using Igor.Text;

namespace Igor.Elixir
{
    [CustomAttributes]
    public class HttpServerExampleGenerator : IElixirGenerator
    {
        private class Variable
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public string Guard { get; set; }
            public string Annotation { get; set; }
        }

        public void Generate(ExModel model, Module mod)
        {
            foreach (var service in mod.WebServices)
            {
                if (!service.webServerEnabled) continue;
                GenerateServerExample(model, service, mod);
            }
        }

        private void GenerateServerExample(ExModel model, WebServiceForm service, Module mod)
        {
            var ex = model.File("web.impl.ex.example").Module(ExName.Combine(service.Module.exName, service.exName, "Impl"));

            foreach (var resource in service.Resources)
            {
                ex.Behaviour(ExName.Combine(service.Module.exName, service.exName));
                if (!string.IsNullOrEmpty(service.Annotation))
                    ex.Annotation = service.Annotation;

                var callVars = new List<Variable>();

                if (resource.exConn) {
                    callVars.Add(new Variable {
                        Name = "conn",
                        Type = "Plug.Conn.t()",
                        Guard = "is_map(conn)",
                        // Annotation = "Plug connection",
                    });
                }

                foreach (var httpVar in resource.RequestVariables)
                {
                    callVars.Add(new Variable {
                        Name = httpVar.exName,
                        Type = httpVar.exType,
                        Guard = Helper.ExGuard(httpVar.Type, httpVar.exName),
                        Annotation = httpVar.Annotation,
                    });
                    foreach (var require in Helper.GuardRequires(httpVar.Type)) {
                        ex.Require(require);
                    }
                }

                if (resource.RequestContent != null)
                {
                    callVars.Add(new Variable {
                        Name = resource.RequestContent.exRequestName,
                        Type = resource.RequestContent.exType,
                        Guard = Helper.ExGuard(resource.RequestContent.Type, resource.RequestContent.exRequestName),
                        Annotation = resource.RequestContent.Annotation,
                    });
                }

                callVars.Add(new Variable {
                    Name = "current_user",
                    Type = "Map.t() | nil",
                    Guard = "(is_map(current_user) or current_user === nil)",
                    // Annotation = "Current user",
                });

                var resultTypes = new List<string>();
                foreach (var response in resource.Responses.Where(x => x.StatusCode < 400))
                {
                    var resultVars = new List<Variable>();

                    if (resource.exConn) {
                        resultVars.Add(new Variable {
                            Name = "conn",
                            Type = "Plug.Conn.t()",
                        });
                    }

                    foreach (var httpVar in response.HeadersVariables.Where(h => h.Name != "_"))
                    {
                        resultVars.Add(new Variable {
                            Name = httpVar.exName,
                            Type = httpVar.exType,
                        });
                    }

                    if (response.Content != null) {
                        resultVars.Add(new Variable {
                            Name = response.Content.exVarName("response_content"),
                            Type = response.Content.exType
                        });
                    }

                    string resultType = "";
                    if (resultVars.Count == 1) {
                        resultType = resultVars[0].Type;
                    } else if (resultVars.Count > 1) {
                        resultType = resultVars.Select(v => v.Type).JoinStrings(", ").Quoted("{", "}");
                    } else {
                        resultType = "any";
                    }
                    resultTypes.Add(resultType);
                }

                ex.Block("# ----------------------------------------------------------------------------");

                var r = ExRenderer.Create();

                if (callVars.Any()) {
                    r += $"@spec {resource.exName}(";
                    r ++;
                    r.Blocks(callVars.Select(v => v.Name + " :: " + v.Type), delimiter: ",");
                    r --;
                    r += $") :: {resultTypes.JoinStrings(" | ")} | no_return";
                } else {
                    r += $"@spec {resource.exName}() :: {resultTypes.JoinStrings(" | ")} | no_return";
                }

                r += $"@impl {service.Module.exName}.{service.exName}";
                if (callVars.Any()) {
                    r += $"def {resource.exName}(";
                    r ++;
                    var rows = callVars.Select(v => new[] { v.Name, string.IsNullOrEmpty(v.Annotation) ? "" : "# " + v.Annotation });
                    r.Table(rows, rowDelimiter: ",", delimiterPosition: 0);
                    r --;
                    if (callVars.Any(v => v.Guard != null)) {
                        r += ") when";
                        r ++;
                        r.Blocks(callVars.Where(v => v.Guard != null).Select(v => v.Guard), delimiter: " and");
                        r --;
                    } else {
                        r += ")";
                    }
                } else {
                    r += $"def {resource.exName}()";
                }

                r += "do";
                r ++;

                string rh = resource.Attribute(ExAttributes.RpcHandler);
                if (rh != null) {
                    r += $@"{rh}";
                } else {
                    r += $@"raise ""Resource '{service.Module.exName}.{service.exName}.{resource.Name}' not yet implemented""";
                }

                r --;
                r += "end";

                ex.Function(r.Build()).Annotation = resource.Annotation ?? $"TODO: annotate {ExName.Combine(service.Module.exName, service.exName, resource.Name)}";
            }
        }

    }
}
