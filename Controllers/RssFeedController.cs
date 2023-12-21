using System.Xml.Linq;
using picoblog.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;


namespace picoblog.Controllers;

public class RssFeedController : Controller
{
  [HttpGet]
  [Route("/feed")]
  [Route("/rss")]
  public ContentResult Get()
  {
      XNamespace ns = "http://purl.org/rss/1.0/";
      string title = $"{Config.Title} RSS Feed";
      string domain = Config.Domain;
      string description = Config.Description;

      var rss = new XDocument(
          new XElement("rss", new XAttribute("version", "2.0"),
              new XElement("channel",
                  new XElement("title", title),
                  new XElement("link", domain),
                  new XElement("description", description),
                  from model in Cache.Models
                  where model.Public && model.Visible
                  select new XElement("item",
                      new XElement("title", model.Title),
                      new XElement("link", $"{domain}/post/{model.Date?.Year}/{model.Title}"),
                      new XElement("description",
                          new XCData(GenerateItemHtml(model, domain))
                      ),
                      new XElement("pubDate", model.Date?.ToString("R") ?? string.Empty) // RFC 822 format
                      // model.CoverImage != null ? new XElement("enclosure", new XAttribute("url", $"{domain}/post/{model.Date?.Year}/{model.Title}/{model.CoverImage}")) : null
                  )
              )
          )
      );

      return Content(rss.ToString(), "application/rss+xml");
  }

  private string GenerateItemHtml(MarkdownModel model, string domain = "localhost:8080")
  {
    return $@"
      <style>
        @font-face {{
          font-family: 'TexGyreAdventor';
          src: url('{domain}/fonts/texgyreadventor-regular.otf') format('opentype');
        }}

        @font-face {{
          font-family: 'Comfortaa';
          src: url('{domain}/fonts/Comfortaa-SemiBold.woff2') format('woff2');
        }}
          .my-content h2 {{
            text-transform: capitalize;
            font-family: texgyreadventor, Georgia, Times New Roman, Times, serif !important;
          }}
          .my-content img {{ object-fit: cover;
            /* opacity: 0.7; */
            bottom: 0;
            top: 0;
            position: absolute;
            width: 100vw;
          }}
          .my-content p {{ color: #666; }}
          .center {{display: flex;
            display: flex;
            flex-direction: column;
            align-items: center;
            text-align: center;
            margin-top: 40vw;
            background-color: white;
            width: 100%;
            left: 0;
            position: absolute;
            height: 100%;
          }}
          .date {{
            color: #c2c2c2;
            font-size: 0.8em;
            text-transform: capitalize;
            margin-bottom: 1.5vh;
            font-family: Comfortaa, Georgia, Times New Roman, Times, serif !important;
          }}
          .hero {{
            height: 50vw;
            width: 100%;
            /* background: black; */
            overflow: hidden;
            position: absolute;
            left: 0;
            right: 0;
            top: 0;
            bottom: 0;
            z-index: -1;
          }}
          .description {{
            font-family: Comfortaa, Georgia, Times New Roman, Times, serif !important;
          }}
      </style>
      <div class='my-content'>
        <div class='hero'>
          <img src='{domain}/post/{model.Date?.Year}/{model.Title}/{model.CoverImage}' alt='Cover Image'/>
        </div>
        <div class='center'>
          <h2>{model.Title}</h2>
          <p class='date'>{model.Date?.ToString("dd. MMM yyyy")}</p>
          <div class='description'>
            <p>{model.Description}</p>
          </div>
          <a href='{domain}/post/{model.Date?.Year}/{model.Title}'> Click here to read more</a>
        </div>
      </div>";
  }
}