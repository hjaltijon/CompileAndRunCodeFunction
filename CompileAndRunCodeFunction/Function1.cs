using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis.Emit;
using System.Collections.Generic;
using System.Reflection;

namespace CompileAndRunCodeFunction
{
    public static class CompileAndRunCodeFunction
    {
        [FunctionName("CompileAndRunCodeFunction")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string name = req.Query["name"];

            string code = await new StreamReader(req.Body).ReadToEndAsync();
            //dynamic data = JsonConvert.DeserializeObject(requestBody);
            //string code = data?.code;

            //string code = JsonSerializer.Deserialize<CompileAndRunDTO>(requestBody).Code;


            string result = CompileAndRun(code);

            string responseMessage = result;

            return new OkObjectResult(responseMessage);
        }

        public class CompileAndRunDTO
        {
            public string Code { get; set; }
        }

        public class CodeExecutionResult
        {
            public bool? EcounteredCompilerErrors { get; set; }
            public List<Error> CompilerErrors { get; set; }
        }

        public class Error
        {
            public string Location { get; set; }
            public string Message { get; set; }
        }

        public static string CompileAndRun(string code)
        {
            //baaa
            //code = code.Replace("[!DOUBLE-QUOTES-REPLACED-HERE!]", @"""");





            // define source code, then parse it (to the type used for compilation)
            //SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(@"
            //    using System;

            //    namespace RoslynCompileSample
            //    {
            //        public class Writer
            //        {
            //            public string Write()
            //            {
            //                return ""message"";
            //            }
            //        }
            //    }");
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(code);

            // define other necessary objects for compilation
            string assemblyName = Path.GetRandomFileName();

            //paths to all the framework.dll files
            var allPathsToFrameworkDllFiles = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator);
            List<MetadataReference> references = new List<MetadataReference>();
            foreach (var dllPath in allPathsToFrameworkDllFiles)
            {
                references.Add(MetadataReference.CreateFromFile(dllPath));
            }

            //MetadataReference[] references = new MetadataReference[]
            //{
            //    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            //    MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location)
            //};

            // analyse and generate IL code from syntax tree
            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using (var ms = new MemoryStream())
            {
                // write IL code into memory
                EmitResult result = compilation.Emit(ms);

                if (!result.Success)
                {
                    // handle exceptions
                    IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic =>
                        diagnostic.IsWarningAsError ||
                        diagnostic.Severity == DiagnosticSeverity.Error);

                    var errors = new List<Error>();
                    foreach (Diagnostic diagnostic in failures)
                    {
                        Console.Error.WriteLine("{0}: {1}", diagnostic.Id, diagnostic.GetMessage());

                        var error = new Error();

                        error.Location = code.Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length);
                        error.Message = "{" + diagnostic.Id + "}: {" + diagnostic.GetMessage() + "}";
                        //errors.Add("{" + diagnostic.Id + "}: {" + diagnostic.GetMessage() + "}");
                        //errors.Add(diagnostic.ToString());
                        errors.Add(error);
                    }
                    var ceResult = new CodeExecutionResult();
                    ceResult.EcounteredCompilerErrors = true;
                    ceResult.CompilerErrors = errors;
                    return JsonSerializer.Serialize(ceResult);
                }
                else
                {
                    // load this 'virtual' DLL so that we can use
                    ms.Seek(0, SeekOrigin.Begin);
                    Assembly assembly = Assembly.Load(ms.ToArray());

                    // create instance of the desired class and call the desired function
                    Type type = assembly.GetType("TeacherTestingNameSpace.TeacherTestingClass");
                    object obj = Activator.CreateInstance(type);
                    //string asdf = (string) type.InvokeMember("Write",
                    //    BindingFlags.Default | BindingFlags.InvokeMethod,
                    //    null,
                    //    obj,
                    //    new object[] { "Hello World" });

                    try
                    {
                        string executionResult = (string)type.InvokeMember("TeacherTestingFunction",
                        BindingFlags.Default | BindingFlags.InvokeMethod,
                        null,
                        obj,
                        null);
                        return "{\"Tests\":" + executionResult + "}";
                    }
                    catch (Exception ex)
                    {
                        var errors = new List<Error>();
                        var error = new Error();
                        error.Location = "";
                        error.Message = ex.InnerException?.Message;
                        errors.Add(error);
                        var ceResult = new CodeExecutionResult();
                        ceResult.EcounteredCompilerErrors = true;
                        ceResult.CompilerErrors = errors;
                        return JsonSerializer.Serialize(ceResult);
                    }
                    
                }
            }
        }


    }
}
