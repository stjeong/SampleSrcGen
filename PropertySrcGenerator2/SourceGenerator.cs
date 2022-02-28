using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using PropertySrcGenerator;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Text;

namespace PropertySrcGenerator2
{
    [Generator]
    public class SourceGenerator : IIncrementalGenerator
    {
        [DllImport("kernel32.dll")]
        internal static extern void OutputDebugString(string lpOutputString);

        public const string AutoPropAttribute = @"
namespace System
{
    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
    public class AutoPropAttribute : System.Attribute
    {
    }
}";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            OutputDebugString("PropertySrcGenerator2.SourceGenerator called!");

            context.RegisterPostInitializationOutput((ctx) =>
            {
                ctx.AddSource("AutoPropAttribute.g.cs", SourceText.From(AutoPropAttribute, Encoding.UTF8));
            });

            IncrementalValuesProvider<ClassDeclarationSyntax> classDeclarations = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (s, _) => IsSyntaxTargetForGeneration(s), // select class with attributes
                    transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx)) // select the class with the [AutoProp] attribute
                .Where(static m => m is not null)!; // filter out attributed classes that we don't care about

            // 선택한 class를 `Compilation`과 결합
            IncrementalValueProvider<(Compilation, ImmutableArray<ClassDeclarationSyntax>)> compilationAndClass
                = context.CompilationProvider.Combine(classDeclarations.Collect());

            // Compilation 및 class를 사용하여 소스 생성
            context.RegisterSourceOutput(compilationAndClass,
                static (spc, source) => Execute(source.Item1, source.Item2, spc));
        }

        static void Execute(Compilation compilation, ImmutableArray<ClassDeclarationSyntax> classes, SourceProductionContext context)
        {
            OutputDebugString("======================= PropertySrcGenerator2.Execute");
            if (classes.IsDefaultOrEmpty)
            {
                return;
            }

            IEnumerable<ClassDeclarationSyntax> distinctClasses = classes.Distinct();

            foreach (var cls in distinctClasses)
            {
                List<AutoFieldInfo> fieldList = GetFieldList(compilation, cls);
                if (fieldList.Count == 0)
                {
                    continue;
                }

                string clsNamespace = GetNamespace(compilation, cls);

                string src = GenerateSource(clsNamespace, cls.Identifier.ValueText, fieldList);
                context.AddSource($"{cls.Identifier.ValueText}.g.cs", SourceText.From(src, Encoding.UTF8));
            }
        }

        static bool IsSyntaxTargetForGeneration(SyntaxNode node)
        {
            OutputDebugString("======================= PropertySrcGenerator2.IsSyntaxTargetForGeneration");

            return node is ClassDeclarationSyntax m && m.AttributeLists.Count > 0;
        }

        private const string EnumExtensionsAttribute = "NetEscapades.EnumGenerators.EnumExtensionsAttribute";

        static ClassDeclarationSyntax? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
        {
            OutputDebugString("======================= PropertySrcGenerator2.GetSemanticTargetForGeneration");

            var classDeclarationSyntax = (ClassDeclarationSyntax)context.Node;

            foreach (AttributeListSyntax attributeListSyntax in classDeclarationSyntax.AttributeLists)
            {
                foreach (AttributeSyntax attributeSyntax in attributeListSyntax.Attributes)
                {
                    if (context.SemanticModel.GetSymbolInfo(attributeSyntax).Symbol is not IMethodSymbol attributeSymbol)
                    {
                        continue;
                    }

                    INamedTypeSymbol attributeContainingTypeSymbol = attributeSymbol.ContainingType;
                    string fullName = attributeContainingTypeSymbol.ToDisplayString();

                    if (fullName == "System.AutoPropAttribute")
                    {
                        return classDeclarationSyntax;
                    }
                }
            }

            return null;
        }





        static private string GetNamespace(Compilation compilation, ClassDeclarationSyntax cls)
        {
            var model = compilation.GetSemanticModel(cls.SyntaxTree);

            foreach (NamespaceDeclarationSyntax ns in cls.Ancestors().OfType<NamespaceDeclarationSyntax>())
            {
                return ns.Name.ToString();
            }

            return "";
        }

        static private string GenerateSource(string clsNamespace, string className, List<AutoFieldInfo> fieldList)
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

        static private string GetSafeFieldName(string identifier)
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

        static private List<AutoFieldInfo> GetFieldList(Compilation compilation, ClassDeclarationSyntax cls)
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

}