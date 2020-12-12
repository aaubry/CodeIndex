﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CodeIndex.Common;
using CodeIndex.IndexBuilder;
using CodeIndex.MaintainIndex;
using CodeIndex.Search;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Core;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Search;
using Microsoft.AspNetCore.Mvc;

namespace CodeIndex.Server.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class LuceneController : ControllerBase
    {
        public LuceneController(CodeIndexConfiguration codeIndexConfiguration, ILog log, CodeIndexSearcherLight codeIndexSearcher)
        {
            this.codeIndexConfiguration = codeIndexConfiguration;
            this.log = log;
            this.codeIndexSearcher = codeIndexSearcher;
        }

        readonly CodeIndexConfiguration codeIndexConfiguration;
        readonly ILog log;
        private readonly CodeIndexSearcherLight codeIndexSearcher;

        [HttpGet]
        public FetchResult<IEnumerable<CodeSource>> GetCodeSources(string searchQuery, bool preview, string indexName, string contentQuery = "", int? showResults = 0)
        {
            FetchResult<IEnumerable<CodeSource>> result;

            try
            {
                indexName.RequireNotNullOrEmpty(nameof(indexName));
                searchQuery.RequireNotNullOrEmpty(nameof(searchQuery));

                var showResultsValue = showResults.HasValue && showResults.Value <= codeIndexConfiguration.MaximumResults && showResults.Value > 0 ? showResults.Value : 100;

                result = new FetchResult<IEnumerable<CodeSource>>
                {
                    Result = SearchCodeSource(searchQuery, out var query, indexName, showResultsValue),
                    Status = new Status
                    {
                        Success = true
                    }
                };

                if (preview)
                {
                    foreach (var item in result.Result)
                    {
                        item.Content = codeIndexSearcher.GenerateHtmlPreviewText(contentQuery, item.Content, 30, indexName);
                    }
                }
                else if (!preview)
                {
                    foreach (var item in result.Result)
                    {
                        item.Content = codeIndexSearcher.GenerateHtmlPreviewText(contentQuery, item.Content, int.MaxValue, indexName, returnRawContentWhenResultIsEmpty: true);
                    }
                }

                log.Debug($"Request: '{searchQuery}' successful, return matched code source");
            }
            catch (Exception ex)
            {
                result = new FetchResult<IEnumerable<CodeSource>>
                {
                    Status = new Status
                    {
                        Success = false,
                        StatusDesc = ex.ToString()
                    }
                };

                log.Error(ex.ToString());
            }

            return result;
        }

        [HttpGet]
        public FetchResult<IEnumerable<CodeSourceWithMatchedLine>> GetCodeSourcesWithMatchedLine(string searchQuery, string indexName, string contentQuery = "", int? showResults = 0, bool needReplaceSuffixAndPrefix = true, bool forWeb = true)
        {
            FetchResult<IEnumerable<CodeSourceWithMatchedLine>> result;

            try
            {
                searchQuery.RequireNotNullOrEmpty(nameof(searchQuery));

                var showResultsValue = showResults.HasValue && showResults.Value <= codeIndexConfiguration.MaximumResults && showResults.Value > 0 ? showResults.Value : 100;

                var codeSources = SearchCodeSource(searchQuery, out var query, indexName, showResultsValue);

                var queryForContent = string.IsNullOrWhiteSpace(contentQuery) ? null : codeIndexSearcher.GetQueryFromStr(contentQuery, indexName);

                var maxContentHighlightLength = codeIndexConfiguration.MaxContentHighlightLength;

                if (maxContentHighlightLength <= 0)
                {
                    maxContentHighlightLength = Constants.DefaultMaxContentHighlightLength;
                }

                var codeSourceWithMatchedLineList = new List<CodeSourceWithMatchedLine>();

                result = new FetchResult<IEnumerable<CodeSourceWithMatchedLine>>
                {
                    Result = codeSourceWithMatchedLineList,
                    Status = new Status
                    {
                        Success = true
                    }
                };

                if (queryForContent != null)
                {
                    var totalResult = 0;

                    foreach (var codeSource in codeSources)
                    {
                        var matchedLines = codeIndexSearcher.GeneratePreviewTextWithLineNumber(queryForContent, codeSource.Content, int.MaxValue, showResultsValue - totalResult, indexName, forWeb: forWeb, needReplaceSuffixAndPrefix: needReplaceSuffixAndPrefix);
                        codeSource.Content = string.Empty; // Empty content to reduce response size

                        foreach (var matchedLine in matchedLines)
                        {
                            totalResult++;

                            codeSourceWithMatchedLineList.Add(new CodeSourceWithMatchedLine(codeSource, matchedLine.LineNumber, matchedLine.MatchedLineContent));
                        }
                    }
                }
                else
                {
                    codeSourceWithMatchedLineList.AddRange(codeSources.Select(u =>
                    {
                        u.Content = string.Empty; // Empty content to reduce response size
                        return new CodeSourceWithMatchedLine(u, 1, string.Empty);
                    }));
                }

                log.Debug($"Request: '{searchQuery}' successful, return matched code source with line number");
            }
            catch (Exception ex)
            {
                result = new FetchResult<IEnumerable<CodeSourceWithMatchedLine>>
                {
                    Status = new Status
                    {
                        Success = false,
                        StatusDesc = ex.ToString()
                    }
                };

                log.Error(ex.ToString());
            }

            return result;
        }

        [HttpGet]
        public FetchResult<IEnumerable<string>> GetHints(string word, string indexName)
        {
            FetchResult<IEnumerable<string>> result;
            try
            {
                word.RequireNotNullOrEmpty(nameof(word));

                result = new FetchResult<IEnumerable<string>>
                {
                    Result = codeIndexSearcher.GetHints(word, indexName),
                    Status = new Status
                    {
                        Success = true
                    }
                };

                log.Debug($"Get Hints For '{word}' successful");
            }
            catch (Exception ex)
            {
                result = new FetchResult<IEnumerable<string>>
                {
                    Status = new Status
                    {
                        Success = false,
                        StatusDesc = ex.ToString()
                    }
                };

                log.Error(ex.ToString());
            }

            return result;
        }

        CodeSource[] SearchCodeSource(string searchStr, out Query query, string indexName, int showResults = 100)
        {
            return codeIndexSearcher.SearchCode(searchStr, out query, showResults, indexName);
        }

        [HttpGet]
        public FetchResult<string> GetTokenizeStr(string searchStr)
        {
            return new FetchResult<string>()
            {
                Result = GetTokenStr(new StandardAnalyzer(Constants.AppLuceneVersion), searchStr) + Environment.NewLine
                + GetTokenStr(new WhitespaceAnalyzer(Constants.AppLuceneVersion), searchStr) + Environment.NewLine
                + GetTokenStr(new SimpleAnalyzer(Constants.AppLuceneVersion), searchStr) + Environment.NewLine
                + GetTokenStr(new StopAnalyzer(Constants.AppLuceneVersion), searchStr) + Environment.NewLine
                + GetTokenStr(new CodeAnalyzer(Constants.AppLuceneVersion, true), searchStr) + Environment.NewLine
            };
        }

        string GetTokenStr(Analyzer analyzer, string content)
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine(analyzer.GetType().FullName);

            var tokenStream = analyzer.GetTokenStream("A", content ?? string.Empty);

            var termAttr = tokenStream.GetAttribute<ICharTermAttribute>();

            tokenStream.Reset();

            while (tokenStream.IncrementToken())
            {
                stringBuilder.AppendLine(termAttr.ToString());
            }

            return stringBuilder.ToString();
        }

        [HttpGet]
        public async Task<FetchResult<string>> GetLogs()
        {
            FetchResult<string> result;
            try
            {
                var logPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "Logs", "CodeIndex.log");

                result = new FetchResult<string>
                {
                    Status = new Status
                    {
                        Success = true
                    }
                };

                if (System.IO.File.Exists(logPath))
                {
                    await using var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var sr = new StreamReader(fs, Encoding.Default);
                    result.Result = await sr.ReadToEndAsync();
                    result.Result = result.Result.SubStringSafe(result.Result.Length - 100000, 100000);
                }
                else
                {
                    result.Result = $"Log Not Exist In {logPath}";
                }
            }
            catch (Exception ex)
            {
                result = new FetchResult<string>
                {
                    Status = new Status
                    {
                        Success = false,
                        StatusDesc = ex.ToString()
                    }
                };

                log.Error(ex.ToString());
            }

            return result;
        }

        [HttpGet]
        public async Task<FetchResult<string[]>> GetIndexNameList([FromServices] IndexManagement indexManagement)
        {
            return await Task.FromResult(indexManagement.GetIndexNameList());
        }
    }
}
