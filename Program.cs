using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.DevTools.V142.CacheStorage;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System.Diagnostics;
using System.Globalization;
using System.Web;

MedicalProductParser parser = new MedicalProductParser();
//await parser.LoadAsync();
await parser.LoadBySeleniumAsync();
parser.Save();

public class MedicinalProduct : IEquatable<MedicinalProduct>
{
	[Name("Идентификатор")]
	public string Id { get; set; } = null!;
	[Name("Наименование")]
	public string Name { get; set; } = null!;
	[Name("По рецепту")]
	public string? ByPrescription { get; set; }
	[Name("Производитель")]
	public string? Manufacturer { get; set; }
	[Name("Действующее вещество")]
	public string? ActiveSubstance { get; set; }
	[Name("Цена")]
	public string Price { get; set; } = null!;
	[Name("Старая цен")]
	public string? OldPrice { get; set; }
	[Name("Изображение")]
	public string? Picture { get; set; }
	[Name("Ссылка")]
	public string Reference { get; set; } = null!;
	[Name("Страна")]
	public string? Country { get; set; }
	public MedicinalProduct()
	{
		Id = Guid.NewGuid().ToString("N");
	}
	public MedicinalProduct(
		string reference,
		string name,
		string price,
		string? oldPrice = "",
		string? byPrescription = "",
		string? manufacturer = "",
		string? activeSubstance = "",
		string? picture = "",
		string? country = "") : this()
	{
		Name = name;
		ByPrescription = byPrescription;
		Manufacturer = manufacturer;
		ActiveSubstance = activeSubstance;
		Price = price;
		OldPrice = oldPrice;
		Picture = picture;
		Reference = reference;
		Country = country;
	}
	public override int GetHashCode()
	{
		return Id.GetHashCode();
	}
	public override bool Equals(object? obj)
	{
		return Equals(obj);
	}
	public bool Equals(MedicinalProduct? other)
	{
		return Object.ReferenceEquals(this, other) || GetHashCode() == other?.GetHashCode();
	}
	public override string ToString()
	{
		return $@"
	Идентификатор: {Id}
	Наименование: {Name}
	По рецепту: {ByPrescription}
	Производитель: {Manufacturer}
	Действующее вещество: {ActiveSubstance}
	Цена: {Price}
	Старая цена: {OldPrice}
	Изображение: {Picture}
	Страна: {Country}
	Ссылка: {Reference}";
	}
	public string ToCsvString()
	{
		string? csvString = "";
		try
		{
			IEnumerable<string>? propertyValues = typeof(MedicinalProduct).GetProperties()?.Select(curPropInfo => curPropInfo?.GetValue(this) as string ?? "");
			if(propertyValues?.Count() > 0)
			{
				csvString = string.Join(',', propertyValues);
			}
		}
		catch(Exception e)
		{
			Debug.WriteLine(e);
			throw;
		}
		return csvString;
	}
}
public static class IWebDriverExtension
{
	public static IWebElement? TryFindElement(this IWebDriver? driver, By by)
	{
		IWebElement? resultWebElement = null;
		try
		{
			IList<IWebElement>? webElements = driver?.FindElements(by);
			if(webElements?.Count > 0)
			{
				resultWebElement = webElements[0];
			}
		}
		catch(StaleElementReferenceException)
		{
			resultWebElement = null;
		}
		catch(NoSuchElementException)
		{
			resultWebElement = null;
		}
		catch(Exception e)
		{
			Debug.WriteLine(e);
			throw;
		}
		return resultWebElement;
	}
}
public static class IWebElementExtension
{
	public static IWebElement? TryFindElement(this IWebElement? thisWebElement, By by)
	{
		IWebElement? resultWebElement = null;
		try
		{
			IList<IWebElement>? webElements = thisWebElement?.FindElements(by);
			if(webElements?.Count > 0)
			{
				resultWebElement = webElements[0];
			}
		}
		catch(StaleElementReferenceException)
		{
			resultWebElement = null;
		}
		catch(NoSuchElementException)
		{
			resultWebElement = null;
		}
		catch(Exception e)
		{
			resultWebElement = null;
			Debug.WriteLine(e);
			throw;
		}
		return resultWebElement;
	}
}
public class MedicalProductParser
{
	public const string Region = "Калининград";
	public static Url SiteUrl = new Url("https://gorzdrav.org/category/sredstva-ot-diabeta");
	public static string FilePath = Path.Combine(AppContext.BaseDirectory, "..\\..\\..\\MedicalProducts.csv");
	public static string QueryCatalogScript = @"return document.querySelector(""div.product-list.product-list--display-grid.product-list--theme--gz"")";
	public static string QueryArrowScript = @"return document.querySelector(""li.ui-table-pagination__arrow-container:last-of-type"")";
	public static string GetOuterHtmlScript = "return arguments[0].ownerDocument.outerHTML";
	public ChromeDriver WebDriver { get; }
	IJavaScriptExecutor ScriptExecutor => WebDriver;
	public WebDriverWait Wait { get; }   
	public IBrowsingContext Context { get; }
	public HtmlParser Parser { get; }
	public IHtmlDocument EmptyHtmlDocument { get; }
	public static CsvConfiguration CsvConfiguration { get; } = new CsvConfiguration(CultureInfo.CurrentCulture)
	{
		Delimiter = CultureInfo.CurrentCulture.TextInfo.ListSeparator,
		HasHeaderRecord = true,
		NewLine = "\r\n"
	};
	public List<MedicinalProduct> MedicinalProducts { get; }
	public MedicalProductParser()
	{
		ChromeOptions options = new ChromeOptions();
		options.AddArgument("--headless");
		WebDriver = new ChromeDriver(options);
		Wait = new WebDriverWait(WebDriver, TimeSpan.FromSeconds(200));
		Context = BrowsingContext.New(Configuration.Default);
		Parser = new HtmlParser(new HtmlParserOptions() { IsScripting = true }, Context);
		EmptyHtmlDocument = Parser.ParseDocument("");
		MedicinalProducts = new List<MedicinalProduct>();
	}
	public async Task LoadAsync()
	{
		try
		{
			MedicinalProducts.Clear();
			await WebDriver.Navigate().GoToUrlAsync(SiteUrl.ToString());
			IWebElement? arrowWebElement = null;
			int currentPage = 0;
			int activePage = 0;
			do
			{
				Console.WriteLine($"Current page: {++currentPage}");
				IWebElement? catalogWebElement = null;
				try
				{
					bool isNextPage = false;
					while(!(isNextPage = Wait.Until(curdriver =>
					{
						int pageNumber = 0;
						IList<IWebElement>? webElements = curdriver.FindElements(By.CssSelector("a.ui-table-pagination__page-item.ui-table-pagination__page-item--clickable.ui-table-pagination__page-item--active"));
						if(webElements?.Count > 0 && webElements[0] != null)
						{
							IWebElement activePageWebElement = webElements[0];
							string? strPageNumber = activePageWebElement.GetDomProperty("textContent");
							pageNumber = !string.IsNullOrWhiteSpace(strPageNumber) ? int.Parse(strPageNumber) : 0;
						}
						activePage = pageNumber;
						return pageNumber == currentPage;
					})));
					if(isNextPage)
					{
						try
						{
							if(Wait.Until(curdriver => curdriver.FindElements(By.CssSelector("div.product-card.product-card--grid.product-card--theme--gz"))?.Count > 20))
							{
								catalogWebElement = WebDriver.FindElement(By.CssSelector("div.product-list.product-list--display-grid.product-list--theme--gz"));
							}
						}
						catch(WebDriverTimeoutException)
						{
							catalogWebElement = null;
						}
						try
						{
							arrowWebElement = Wait.Until<IWebElement?>(curdriver => (curdriver as IJavaScriptExecutor)?.ExecuteScript(QueryArrowScript) as IWebElement);
						}
						catch(WebDriverTimeoutException)
						{
							arrowWebElement = null;
						}
					}
				}
				catch(WebDriverTimeoutException)
				{
				}
				Console.WriteLine($"Active page: {activePage}"); 
				if(catalogWebElement != null)
				{
					//await Task.Delay(4000);
					string? outerHTML = catalogWebElement.GetDomProperty("outerHTML");
					if(!string.IsNullOrWhiteSpace(outerHTML))
					{
						INodeList? catalogNodeList = Parser.ParseFragment(outerHTML, EmptyHtmlDocument.Body!);
						IHtmlDivElement? catalogDivElement = Parser.ParseFragment(outerHTML, EmptyHtmlDocument.Body!)?.Cast<IHtmlDivElement>()?.FirstOrDefault();
						string strMessage = catalogDivElement != null ? $"Catalog is found." : "Catalog is not found";
						Console.WriteLine(strMessage);
						if(catalogDivElement != null)
						{
							//List<IHtmlDivElement>? productDivElements = catalogDivElement.QuerySelectorAll("div.product-card.product-card--grid.product-card--theme--gz")?
							//	.Cast<IHtmlDivElement>()?.ToList();
							List<IHtmlDivElement>? productDivElements = catalogDivElement.Children.Cast<IHtmlDivElement>()?.ToList();
							strMessage = $"There are {(productDivElements?.Count() > 0 ? productDivElements.Count() : "no")} DIV elements in the catalog.";
							Console.WriteLine(strMessage);
							if(productDivElements?.Count() > 0)
							{
								foreach(IHtmlDivElement productDivElement in productDivElements)
								{
									int index = productDivElements.Index(productDivElement);
									IElement? prescriptionElement = productDivElement.QuerySelector("div.product-card-body.product-card-body--theme--gz > div");
									IElement? nameElement = productDivElement.QuerySelector("a.product-card-body__title.product-card-body__title--url.product-card-body__title--theme--gz");
									IElement? priceElement = productDivElement.QuerySelector("span.ui-price__price.ui-price__price--discount.ui-price__price--theme--gz");
									IElement? oldPriceElement = productDivElement.QuerySelector("span.ui-price__discount-value.ui-price__discount-value--theme--gz");
									IElement? pictureElement = productDivElement.QuerySelector("img.product-card-image__img.product-card-image__img--theme--gz");
									IElement? referenceElement = productDivElement.QuerySelector("div.product-card-poster.product-card-poster--theme--gz a");
									IHtmlCollection<IElement>? descriptionElements = productDivElement.QuerySelectorAll("div.product-card__item.product-card__item--theme--gz");
									if(!string.IsNullOrWhiteSpace(nameElement?.TextContent) && !string.IsNullOrWhiteSpace(priceElement?.TextContent)
										&& referenceElement is IHtmlAnchorElement referenceAnchorElement && !string.IsNullOrWhiteSpace(referenceAnchorElement.Href))
									{
										MedicinalProduct medicinalProduct = new MedicinalProduct();
										medicinalProduct.Reference = referenceAnchorElement.Href.Trim();
										medicinalProduct.Name = nameElement.TextContent.Trim();
										medicinalProduct.ByPrescription = "";
										if(prescriptionElement != null && prescriptionElement.TextContent?.Contains("По рецепту") == true)
										{
											medicinalProduct.ByPrescription = "По рецепту";
										}
										medicinalProduct.Price = priceElement.TextContent;
										medicinalProduct.OldPrice = oldPriceElement?.TextContent;
										if(pictureElement is IHtmlImageElement pictureImageElement)
										{
											medicinalProduct.Picture = pictureImageElement.Source;
										}
										if(descriptionElements?.Count > 0)
										{
											IElement? manufacturerElement = descriptionElements.FirstOrDefault(element => element.TextContent.Contains("Производитель:"));
											IElement? activeSubstanceElement = descriptionElements.FirstOrDefault(element => element.TextContent.Contains("Действующее вещество:"));
											IElement? countryElement = descriptionElements.FirstOrDefault(element => element.TextContent.Contains("Страна:"));
											medicinalProduct.Manufacturer = manufacturerElement?.TextContent?.Replace("Производитель:", "")?.Trim();
											medicinalProduct.ActiveSubstance = activeSubstanceElement?.TextContent?.Replace("Действующее вещество:", "")?.Trim();
											medicinalProduct.Country = countryElement?.TextContent?.Replace("Страна:", "")?.Trim();
										}
										MedicinalProducts.Add(medicinalProduct);
										Console.WriteLine(medicinalProduct);
									}
								}
							}
						}
					}
				}
				//arrowWebElement = Wait.Until(ExpectedConditions.ElementToBeClickable(By.CssSelector("li.ui-table-pagination__arrow-container:last-of-type")));
				if(arrowWebElement != null)
				{
					//ScriptExecutor.ExecuteScript("setTimeout(function(){ element.click(); }, 100, arguments[0]);", arrowWebElement);
					ScriptExecutor.ExecuteScript("arguments[0].click();", arrowWebElement);
					//await Task.Delay(1000);
				}
			}
			while(arrowWebElement != null);
			WebDriver.Quit();
		}
		catch(Exception e)
		{
			Debug.WriteLine(e);
			throw;
		}
	}
	public async Task LoadBySeleniumAsync()
	{
		try
		{
			MedicinalProducts.Clear();
			await WebDriver.Navigate().GoToUrlAsync(SiteUrl.ToString());
			List<IWebElement>? productWebElements = null;
			IWebElement[]? arrOldProductWebElements = null;
			List<IWebElement>? oldProductWebElements = null;
			IWebElement? arrowWebElement = null;
			int currentPage = 0;
			int activePage = 0;
			do
			{
				Console.WriteLine($"Current page: {++currentPage}");
				if(productWebElements?.Count > 0)
				{
					arrOldProductWebElements = new IWebElement[productWebElements.Count];
					productWebElements.ToArray().CopyTo(arrOldProductWebElements, 0);
					productWebElements.Clear();
				}
				oldProductWebElements = arrOldProductWebElements?.ToList();
				productWebElements = null;
				try
				{
					bool isNextPage = currentPage == 1 && activePage <= currentPage
						|| Wait.Until(driver =>
						{
							activePage = int.Parse(HttpUtility.ParseQueryString(new Url(driver.Url).Query ?? "")?.Get("page") ?? "0");
							return activePage == currentPage;
						});
					if(isNextPage)
					{
						try
						{
							productWebElements = Wait.Until(ExpectedConditions.VisibilityOfAllElementsLocatedBy(By.CssSelector("div.product-card.product-card--grid.product-card--theme--gz")))?.ToList();
						}
						catch(StaleElementReferenceException)
						{
						}
						catch(NoSuchElementException)
						{
						}
						catch(WebDriverTimeoutException)
						{
							productWebElements = null;
						}
						try
						{
							arrowWebElement = Wait.Until<IWebElement?>(curdriver => curdriver.TryFindElement(By.CssSelector("li.ui-table-pagination__arrow-container:last-of-type")));
						}
						catch(StaleElementReferenceException)
						{
						}
						catch(NoSuchElementException)
						{
						}
						catch(WebDriverTimeoutException)
						{
							arrowWebElement = null;
						}
					}
				}
				catch(WebDriverTimeoutException)
				{
				}
				Console.WriteLine($"Active page: {activePage}");
				if(productWebElements?.Count > 0)
				{
					//foreach(IWebElement productWebElement in productWebElements)
					for(int index = 1; index <= productWebElements.Count; index++)
					{
						//int index = productWebElements.IndexOf(productWebElement);
						Console.WriteLine($"Индекс: {index}");
						IWebElement? productWebElement = Wait.Until(driver => driver.TryFindElement(By.CssSelector(
							$"div.product-list.product-list--display-grid.product-list--theme--gz :nth-child({index} of div.product-card.product-card--grid.product-card--theme--gz)")));
						if(productWebElement != null)
						{
							IWebElement? prescriptionWebElement = productWebElement.TryFindElement(By.CssSelector("div.product-card-body.product-card-body--theme--gz > div"));
							IWebElement? nameWebElement = productWebElement.TryFindElement(By.CssSelector("a.product-card-body__title.product-card-body__title--url.product-card-body__title--theme--gz"));
							IWebElement? priceWebElement = productWebElement.TryFindElement(By.CssSelector("span.ui-price__price.ui-price__price--discount.ui-price__price--theme--gz"));
							IWebElement? oldPriceWebElement = productWebElement.TryFindElement(By.CssSelector("span.ui-price__discount-value.ui-price__discount-value--theme--gz"));
							IWebElement? pictureWebElement = productWebElement.TryFindElement(By.CssSelector("img.product-card-image__img.product-card-image__img--theme--gz"));
							IWebElement? referenceWebElement = productWebElement.TryFindElement(By.CssSelector("div.product-card-poster.product-card-poster--theme--gz a"));
							IList<IWebElement>? descriptionWebElements = productWebElement.FindElements(By.CssSelector("div.product-card__item.product-card__item--theme--gz"));
							string? name = nameWebElement?.GetDomProperty("textContent");
							string? price = priceWebElement?.GetDomProperty("textContent");
							string? oldPrice = oldPriceWebElement?.GetDomProperty("textContent");
							string? reference = referenceWebElement?.GetDomProperty("href");
							if(!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(price) && !string.IsNullOrWhiteSpace(reference))
							{
								MedicinalProduct medicinalProduct = new MedicinalProduct(reference, name, price, oldPrice)
								{
									Picture = pictureWebElement?.GetDomProperty("src")
								};
								medicinalProduct.ByPrescription = "";
								if(prescriptionWebElement != null && prescriptionWebElement.GetDomProperty("textContent")?.Contains("По рецепту") == true)
								{
									medicinalProduct.ByPrescription = "По рецепту";
								}
								if(descriptionWebElements?.Count > 0)
								{
									IWebElement? manufacturerWebElement = descriptionWebElements.FirstOrDefault(element => element.GetDomProperty("textContent")?.Contains("Производитель:") == true);
									IWebElement? activeSubstanceWebElement = descriptionWebElements.FirstOrDefault(element => element.GetDomProperty("textContent")?.Contains("Действующее вещество:") == true);
									IWebElement? countryWebElement = descriptionWebElements.FirstOrDefault(element => element.GetDomProperty("textContent")?.Contains("Страна:") == true);
									medicinalProduct.Manufacturer = manufacturerWebElement?.GetDomProperty("textContent")?.Replace("Производитель:", "")?.Trim();
									medicinalProduct.ActiveSubstance = activeSubstanceWebElement?.GetDomProperty("textContent")?.Replace("Действующее вещество:", "")?.Trim();
									medicinalProduct.Country = countryWebElement?.GetDomProperty("textContent")?.Replace("Страна:", "")?.Trim();
								}
								MedicinalProducts.Add(medicinalProduct);
								Console.WriteLine(medicinalProduct);
							}
						}
					}
				}
				if(arrowWebElement != null)
				{
					ScriptExecutor.ExecuteScript("arguments[0].click();", arrowWebElement);
				}
			}
			while(arrowWebElement != null);
			WebDriver.Quit();
		}
		catch(Exception e)
		{
			Debug.WriteLine(e);
			throw;
		}
	}
	public async void Save(bool byRegion = false)
	{
		try
		{
			IEnumerable<MedicinalProduct> medicinalProducts =
				byRegion ? MedicinalProducts.Where(medicinalProduct => medicinalProduct?.Country?.Contains(Region) == true) : MedicinalProducts;
			if(medicinalProducts.Count() > 0)
			{
				using(StreamWriter streamWriter = new StreamWriter(FilePath))
				{
					using(CsvWriter csvWriter = new CsvWriter(streamWriter, CsvConfiguration))
					{
						csvWriter.WriteHeader<MedicinalProduct>();
						foreach(MedicinalProduct medicinalProduct in medicinalProducts)
						{
							csvWriter.WriteRecord(medicinalProduct);
						}
					}
				}
			}
		}
		catch(Exception e)
		{
			Debug.WriteLine(e);
			throw;
		}
	}
}