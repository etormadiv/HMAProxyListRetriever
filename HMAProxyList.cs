/**
 *   Retrieve Hide My Ass(TM) Privax(R) Proxy List and bypass Anti-Bot Techniques.
 *   Copyright (C) 2016  Etor Madiv
 *
 *   This program is free software: you can redistribute it and/or modify
 *   it under the terms of the GNU General Public License as published by
 *   the Free Software Foundation, either version 3 of the License, or
 *   (at your option) any later version.
 *
 *   This program is distributed in the hope that it will be useful,
 *   but WITHOUT ANY WARRANTY; without even the implied warranty of
 *   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *   GNU General Public License for more details.
 *
 *   You should have received a copy of the GNU General Public License
 *   along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace HMAProxyListClient
{
	public class Program
	{
		public static void Main()
		{
			string rawHtml = "";
			
			try
			{
				HttpWebRequest hwr = (HttpWebRequest) WebRequest.Create("http://proxylist.hidemyass.com/");
				hwr.Method         = "GET";
				hwr.UserAgent      = "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/51.0.2704.103 Safari/537.36";
				//TODO: Uncomment below line and specify a proxy if the website is not available in your country
				//hwr.Proxy          = new WebProxy("47.88.104.219", 80);
				
				using(var hwResponse = (HttpWebResponse) hwr.GetResponse() )
				{
					using(var stream = hwResponse.GetResponseStream() )
					{
						using(var reader = new StreamReader(stream) )
						{
							rawHtml = reader.ReadToEnd();
						}
					}
				}
			}
			catch
			{
				Console.WriteLine("Exception thrown");
				return;
			}
			
			
			HMAProxyList hmaList = new HMAProxyList(rawHtml);
			hmaList.GetTableIndex();
			
			while(true)
			{
				try
				{
					hmaList.GrabNextStyles();
					
					string ip   = "";
					string port = "";
					
					while(true)
					{
						try
						{
							ip += hmaList.ParseNextIpPart();
						}
						catch
						{
							ip = ip.Trim();
							port = hmaList.GetPort();
							break;
						}
					}
					
					Console.WriteLine(ip + ":" + port);
					
				}
				catch
				{
					break;
				}
			}
			
			Console.ReadLine();
		}
	}
	
	public class HMAProxyList
	{
		private string rawHtmlContent;
		
		private HMAIPFixer fixer = new HMAIPFixer();
		
		private int tableIndex;
		
		private int lastStylesIndex;
		
		private bool firstIpPartChild;
		
		private int lastIpPartIndex;
		
		public HMAProxyList(string rawHtml)
		{
			rawHtmlContent = rawHtml;
		}
		
		public void GetTableIndex()
		{
			int index = rawHtmlContent.IndexOf("<table class=\"hma-table\" id=\"listable\"");
			
			if(index < 0)
			{
				throw new Exception("Can not Find Table");
			}
			
			lastStylesIndex = tableIndex = index;
		}
		
		public void GrabNextStyles()
		{
			firstIpPartChild = true;
			
			fixer.Styles.Clear();
			
			int startIndex = rawHtmlContent.IndexOf("<style>", lastStylesIndex) + 7;
			
			if(startIndex < 7)
			{
				throw new Exception("No more styles found");
			}
			
			int endIndex   = rawHtmlContent.IndexOf("</style>", startIndex);
			
			lastStylesIndex = endIndex;
			
			string rawStyles = rawHtmlContent.Substring(startIndex, endIndex - startIndex);
			
			string[] styles = rawStyles.Split('\n');
			
			foreach(string style in styles)
			{
				if(style.Trim() != "")
				{
					fixer.Styles.Add(new HMAStyleClass(style.Trim()));
				}
			}
		}
		
		public string ParseNextIpPart()
		{
			if(firstIpPartChild)
			{
				lastIpPartIndex = lastStylesIndex + 8;
				firstIpPartChild = false;
			}
			
			HMAIPPart part = null;
			
			if(rawHtmlContent[lastIpPartIndex] == '<') /* We either found an opening tag or a final td */
			{
				int index = rawHtmlContent.IndexOf(">", lastIpPartIndex); /* The opening tag close */
				index = rawHtmlContent.IndexOf(">", index + 1);			  /* The closing tag close */
				
				string rawHtmlLine = rawHtmlContent.Substring(lastIpPartIndex, index - lastIpPartIndex + 1);
				
				if(rawHtmlLine.Contains("</td>"))
				{
					throw new Exception("No more IP Parts");
				}
				
				lastIpPartIndex = index + 1;
				
				part = fixer.CheckRawHtml(rawHtmlLine);
				
			}
			else /* We found a raw content */
			{
				int index = rawHtmlContent.IndexOf("<", lastIpPartIndex); /* The opening tag open */
				
				string rawHtmlLine = rawHtmlContent.Substring(lastIpPartIndex, index - lastIpPartIndex);
				
				lastIpPartIndex = index;
				
				part = fixer.CheckRawHtml(rawHtmlLine);
			}
			
			return (part.IsValid) ? (part.Value) : ("");
		}
		
		public string GetPort()
		{
			int startIndex = rawHtmlContent.IndexOf("<td>", lastIpPartIndex) + 4; /* Find next opening td tag */
			int endIndex   = rawHtmlContent.IndexOf("</td>", startIndex);         /* Find next closing td tag */
			string rawHtmlLine = rawHtmlContent.Substring(startIndex, endIndex - startIndex);
			
			return rawHtmlLine.Trim();
		}
	}
	
	public class HMAStyleClass
	{
		public string ClassName;
		public bool   Visible;
		
		public HMAStyleClass(string rawCssLine)
		{
			string rawCss = rawCssLine.Substring(1, rawCssLine.Length - 2);
			string[] array = rawCss.Split('{');
			ClassName = array[0];
			Visible = (array[1] != "display:none");
		}
	}
	
	public class HMAIPPart
	{
		public string Value;
		public bool   IsValid;
	}
	
	public class HMAIPFixer
	{
		public List<HMAStyleClass> Styles = new List<HMAStyleClass>();
		
		public HMAIPPart CheckRawHtml(string rawHtmlLine)
		{
			HMAIPPart part = new HMAIPPart();
			
			if(rawHtmlLine.Contains("class") || rawHtmlLine.Contains("style") )
			{
				int classIndex = rawHtmlLine.IndexOf("class=\"");
				int styleIndex = rawHtmlLine.IndexOf("style=\"");
				
				int length = 0;
				
				if(classIndex > 0) /*We found a class*/
				{
					length = rawHtmlLine.IndexOf("\"", classIndex + 7) - (classIndex + 7);
					
					string classValue = rawHtmlLine.Substring(classIndex + 7, length);
					
					part.Value = GetInnerText(rawHtmlLine);
					part.IsValid = CheckClassName(classValue);
					
					return part;
				}
				else if (styleIndex > 0) /*We found a style*/
				{
					length = rawHtmlLine.IndexOf("\"", styleIndex + 7) - (styleIndex + 7);
					
					string styleValue = rawHtmlLine.Substring(styleIndex + 7, length);
					
					part.Value = GetInnerText(rawHtmlLine);
					part.IsValid = (styleValue != "display:none");
					
					return part;
				}
				else 
				{
					throw new Exception("Error");
				}
			}
			else if(rawHtmlLine.StartsWith("<")) /*No style or class were given */
			{
				part.Value = GetInnerText(rawHtmlLine);
				part.IsValid = true;
					
				return part;
			}
			else /*It is just a raw text*/
			{
				part.Value = rawHtmlLine;
				part.IsValid = true;
					
				return part;
			}
		}
		
		public bool CheckClassName(string className)
		{
			foreach(HMAStyleClass hmaClass in Styles)
			{
				if(hmaClass.ClassName == className)
				{
					return hmaClass.Visible;
				}
			}
			return true;
		}
		
		public string GetTagName(string rawHtmlLine)
		{
			int delimiterIndex = rawHtmlLine.IndexOf(" ");
			if(delimiterIndex < 0)
			{
				delimiterIndex = rawHtmlLine.IndexOf(">");
			}
			if(delimiterIndex < 0)
			{
				throw new Exception("Unknown tag delimiter");
			}
			
			string tagName = rawHtmlLine.Substring(1, delimiterIndex - 1);
			return tagName;
		}
		
		public string GetInnerText(string rawHtmlLine)
		{
			string tagName = GetTagName(rawHtmlLine);
			
			Regex regex = new Regex("<" + tagName + "[^>]*>(.*?)</" + tagName + ">");

		        if (regex.IsMatch(rawHtmlLine))
		        {
		         	MatchCollection collection = regex.Matches(rawHtmlLine);
		        	foreach (Match m in collection)
		                {
		                    return m.Groups[1].Value;
		                }
		        }
			
			return "";
		}
	}
}
