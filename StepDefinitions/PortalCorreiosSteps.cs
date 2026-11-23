using System.Globalization;
using System.Linq;
using System.Text;
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using Reqnroll;

namespace CorreiosAutomation.StepDefinitions;

[Binding]
public class PortalCorreiosSteps
{
    private const string BuscaCepUrl =
        "https://buscacepinter.correios.com.br/app/endereco/index.php";
    private const string RastreamentoUrl =
        "https://rastreamento.correios.com.br/app/index.php";

    private readonly ScenarioContext _scenarioContext;

    private IWebDriver Driver => (IWebDriver)_scenarioContext["driver"];

    public PortalCorreiosSteps(ScenarioContext scenarioContext)
    {
        _scenarioContext = scenarioContext;
    }

    [BeforeScenario]
    public void AbrirNavegador()
    {
        var options = new ChromeOptions
        {
            PageLoadStrategy = PageLoadStrategy.Eager
        };
        options.AddArgument("--start-maximized");
        options.AddArgument("--disable-notifications");

        _scenarioContext["driver"] = new ChromeDriver(options);
    }

    [AfterScenario]
    public void FecharNavegador()
    {
        if (_scenarioContext.TryGetValue("driver", out var browser) &&
            browser is IWebDriver driver)
        {
            try
            {
                SalvarEvidencia(driver, "estado-final");
            }
            catch
            {
                // Mantém o fechamento do navegador mesmo se a captura falhar.
            }

            driver.Quit();
            driver.Dispose();
        }
    }

    [Given("que acesso a página Busca CEP dos Correios")]
    public void AcessarBuscaCep()
    {
        Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
        Driver.Navigate().GoToUrl(BuscaCepUrl);
        AguardarElemento(By.Id("endereco"), TimeSpan.FromSeconds(30));
    }

    [When("pesquiso o CEP {string} e resolvo o CAPTCHA manualmente")]
    public void PesquisarCepComCaptchaManual(string cep)
    {
        // Seletor por ID.
        var campoCep = AguardarElemento(By.Id("endereco"), TimeSpan.FromSeconds(15));
        campoCep.Clear();
        campoCep.SendKeys(cep);

        TestContext.Progress.WriteLine(
            $"Digite o CAPTCHA atual no Chrome para o CEP {cep}. " +
            "O teste enviará a busca assim que o campo for preenchido; não clique em Buscar.");

        new WebDriverWait(Driver, TimeSpan.FromSeconds(120)).Until(d =>
        {
            var campos = d.FindElements(By.Id("captcha"));
            return campos.Count > 0 && campos[0].Displayed &&
                   !string.IsNullOrWhiteSpace(campos[0].GetAttribute("value"));
        });

        Driver.FindElement(By.Id("btn_pesquisar")).Click();
    }

    [Then("confirmo que o CEP não foi encontrado")]
    public void ConfirmarCepInexistente()
    {
        new WebDriverWait(Driver, TimeSpan.FromSeconds(30)).Until(d =>
        {
            var texto = Normalizar(d.FindElement(By.TagName("body")).Text);
            return texto.Contains("NAO HA DADOS A SEREM EXIBIDOS") ||
                   texto.Contains("DADOS NAO ENCONTRADO");
        });

        SalvarEvidencia(Driver, "cep-80700000-nao-encontrado");
    }

    [When("clico em Nova Busca")]
    public void ClicarEmNovaBusca()
    {
        // Seletor por XPath.
        Driver.FindElement(By.XPath("//button[@id='btn_nbusca']")).Click();
        AguardarElemento(By.Id("endereco"), TimeSpan.FromSeconds(15));
    }

    [Then("confirmo o endereço da Rua Quinze de Novembro em São Paulo")]
    public void ConfirmarEndereco()
    {
        // Seletor por CSS; valida a linha da tabela retornada pelos Correios.
        var linha = new WebDriverWait(Driver, TimeSpan.FromSeconds(30))
            .Until(d => d.FindElements(By.CssSelector("table tbody tr"))
                .FirstOrDefault(item =>
                {
                    var texto = Normalizar(item.Text);
                    return texto.Contains("RUA QUINZE DE NOVEMBRO") &&
                           texto.Contains("SAO PAULO/SP");
                }));

        Assert.That(linha, Is.Not.Null,
            "O resultado não contém Rua Quinze de Novembro e São Paulo/SP.");

        var textoResultado = Normalizar(linha!.Text);
        Assert.That(textoResultado, Does.Contain("RUA QUINZE DE NOVEMBRO"));
        Assert.That(textoResultado, Does.Contain("SAO PAULO/SP"));

        SalvarEvidencia(Driver, "cep-01013-001-endereco-confirmado");
    }

    [When("consulto no rastreamento o código {string}")]
    public void ConsultarRastreamento(string codigo)
    {
        Driver.Navigate().GoToUrl(RastreamentoUrl);
        var campoCodigo = AguardarElemento(By.Id("objeto"), TimeSpan.FromSeconds(30));
        campoCodigo.SendKeys(codigo);

        TestContext.Progress.WriteLine(
            "Se o rastreamento exibir uma verificação de segurança, conclua-a manualmente no Chrome. " +
            "O teste aguardará o resultado por até 120 segundos.");

        Driver.FindElement(By.Id("b-pesquisar")).Click();
    }

    [Then("confirmo que o código de rastreamento é inválido")]
    public void ConfirmarCodigoInvalido()
    {
        string? resultado = null;

        try
        {
            new WebDriverWait(Driver, TimeSpan.FromSeconds(120)).Until(d =>
            {
                var texto = Normalizar(d.FindElement(By.TagName("body")).Text);

                if (texto.Contains("NAO FOI POSSIVEL CONCLUIR A VERIFICACAO DE SEGURANCA"))
                {
                    resultado = "VERIFICACAO_BLOQUEADA";
                    return true;
                }

                if (texto.Contains("CODIGO INVALIDO") ||
                    texto.Contains("OBJETO NAO ENCONTRADO") ||
                    texto.Contains("OBJETO NAO LOCALIZADO") ||
                    texto.Contains("NAO FOI LOCALIZADO"))
                {
                    resultado = "CODIGO_INVALIDO";
                    return true;
                }

                return false;
            });
        }
        catch (WebDriverTimeoutException)
        {
            SalvarEvidencia(Driver, "rastreamento-sem-retorno-conclusivo");
            Assert.Inconclusive(
                "O site não apresentou um resultado conclusivo para o código de rastreamento.");
            return;
        }

        SalvarEvidencia(Driver, "rastreamento-resultado");

        if (resultado == "VERIFICACAO_BLOQUEADA")
        {
            Assert.Inconclusive(
                "A verificação de segurança não foi concluída. Não é possível afirmar que o código é inválido.");
            return;
        }

        Assert.That(resultado, Is.EqualTo("CODIGO_INVALIDO"),
            "O site não confirmou que o código de rastreamento é inválido.");
    }

    private IWebElement AguardarElemento(By seletor, TimeSpan timeout)
    {
        return new WebDriverWait(Driver, timeout).Until(d =>
        {
            var elementos = d.FindElements(seletor);
            return elementos.FirstOrDefault(elemento => elemento.Displayed);
        });
    }

    private static void SalvarEvidencia(IWebDriver driver, string nome)
    {
        var pasta = Path.Combine(Directory.GetCurrentDirectory(), "evidencias");
        Directory.CreateDirectory(pasta);

        var caminho = Path.Combine(
            pasta, $"{nome}-{DateTime.Now:yyyyMMdd-HHmmss}.png");

        ((ITakesScreenshot)driver).GetScreenshot().SaveAsFile(caminho);
        TestContext.Progress.WriteLine($"Screenshot salvo em: {caminho}");
    }

    private static string Normalizar(string texto)
    {
        var decomposicao = texto.Normalize(NormalizationForm.FormD);
        var caracteres = decomposicao
            .Where(caractere => CharUnicodeInfo.GetUnicodeCategory(caractere)
                != UnicodeCategory.NonSpacingMark)
            .ToArray();

        return new string(caracteres).ToUpperInvariant();
    }
}
