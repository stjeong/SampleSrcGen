using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using PropertySrcGenerator;
using System.Runtime.InteropServices;
using System.Text;

namespace V1Generator
{
    [Generator]
    public class PropertySrcGenerator : ISourceGenerator
    {
        // [DllImport("kernel32.dll")]
        // internal static extern void OutputDebugString(string lpOutputString);

        public const string AutoPropAttribute = @"
namespace System
{
    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
    public class AutoPropAttribute : System.Attribute
    {
    }
}";

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForPostInitialization((ctx) =>
            {
                ctx.AddSource("AutoPropAttribute.g.cs", SourceText.From(AutoPropAttribute, Encoding.UTF8));
            });

            context.RegisterForSyntaxNotifications(() => new AutoPropSyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            AutoPropSyntaxReceiver? recv = context.SyntaxReceiver as AutoPropSyntaxReceiver;
            if (recv == null)
            {
                return;
            }

            if (recv.AutoPropClasses.Count == 0)
            {
                return;
            }

            foreach (var cls in recv.AutoPropClasses)
            {
                List<AutoFieldInfo> fieldList = GetFieldList(context.Compilation, cls);
                if (fieldList.Count == 0)
                {
                    continue;
                }

                string clsNamespace = GetNamespace(context.Compilation, cls);

                string src = GenerateSource(clsNamespace, cls.Identifier.ValueText, fieldList);
                context.AddSource($"{cls.Identifier.ValueText}.g.cs", SourceText.From(src, Encoding.UTF8));
            }
        }

        private string GetNamespace(Compilation compilation, ClassDeclarationSyntax cls)
        {
            var model = compilation.GetSemanticModel(cls.SyntaxTree);

            foreach (NamespaceDeclarationSyntax ns in cls.Ancestors().OfType<NamespaceDeclarationSyntax>())
            {
                return ns.Name.ToString();
            }

            return "";
        }

        private string GenerateSource(string clsNamespace, string className, List<AutoFieldInfo> fieldList)
        {
            IndentText sb = new IndentText();

            bool hasNamespace = string.IsNullOrEmpty(clsNamespace) == false;

            if (hasNamespace)
            {
                sb.AppendLine($"namespace {clsNamespace}");
                sb.AppendLine("{");
            }

            using (sb.Indent(hasNamespace))
            {
                sb.AppendLine(@$"partial class {className}");
                sb.AppendLine("{");

                using (sb.Indent())
                {
                    sb.Append($"public {className}(", true);
                    int count = 0;
                    foreach (var field in fieldList)
                    {
                        sb.Append($"{(count == 0 ? "" : ", ")}{field.TypeName} {field.Identifier}");
                        count++;
                    }
                    sb.AppendLine(")", false);

                    sb.AppendLine("{");

                    using (sb.Indent())
                    {
                        foreach (var field in fieldList)
                        {
                            sb.AppendLine($"this.{field.Identifier} = {field.Identifier};");
                        }
                    }

                    sb.AppendLine("}");

                    foreach (var field in fieldList)
                    {
                        sb.AppendLine($"public {field.TypeName} {GetSafeFieldName(field.Identifier)} {{ get => {field.Identifier}; set => {field.Identifier} = value; }}");
                    }
                }

                sb.AppendLine("}");
            }

            if (string.IsNullOrEmpty(clsNamespace) == false)
            {
                sb.AppendLine("}");
            }

            return sb.ToString();
        }

        private string GetSafeFieldName(string identifier)
        {
            if (identifier[0] == '_')
            {
                return identifier.Substring(0);
            }

            if (char.IsLower(identifier[0]))
            {
                return identifier[0].ToString().ToUpper() + identifier.Substring(1);
            }

            return identifier.ToUpper();
        }

        private List<AutoFieldInfo> GetFieldList(Compilation compilation, ClassDeclarationSyntax cls)
        {
            List<AutoFieldInfo> fieldList = new List<AutoFieldInfo>();

            var model = compilation.GetSemanticModel(cls.SyntaxTree);

            foreach (FieldDeclarationSyntax field in cls.DescendantNodes().OfType<FieldDeclarationSyntax>())
            {
                foreach (var item in field.Declaration.Variables)
                {
                    AutoFieldInfo info = new AutoFieldInfo
                    {
                        Identifier = item.Identifier.ValueText,
                        TypeName = field.Declaration.Type.ToString()
                    };

                    fieldList.Add(info);
                }
            }

            return fieldList;
        }
    }

    public struct AutoFieldInfo
    {
        public string Identifier;
        public string TypeName;
    }

    class AutoPropSyntaxReceiver : ISyntaxReceiver
    {
        public List<ClassDeclarationSyntax> AutoPropClasses = new List<ClassDeclarationSyntax>();

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (syntaxNode is not ClassDeclarationSyntax cds)
            {
                return;
            }

            foreach (var item in cds.AttributeLists)
            {
                foreach (var attr in item.Attributes)
                {
                    string attrName = attr.Name.ToString();

                    switch (attrName)
                    {
                        case "AutoProp":
                        case "System.AutoProp":
                        case "AutoPropAttribute":
                        case "System.AutoPropAttribute":

                            foreach (var mod in cds.Modifiers)
                            {
                                if (mod.ValueText == "partial")
                                {
                                    AutoPropClasses.Add(cds);
                                    return;
                                }
                            }
                            break;
                    }
                }
            }
        }
    }
}
