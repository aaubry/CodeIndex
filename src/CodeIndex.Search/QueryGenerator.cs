﻿using System;
using CodeIndex.Common;
using CodeIndex.IndexBuilder;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;

namespace CodeIndex.Search
{
    public class QueryGenerator
    {
        public Query GetQuery(SearchCandidate[] searchCandidates)
        {
            searchCandidates.RequireContainsElement(nameof(searchCandidates));

            // search with a phrase
            var query = new BooleanQuery();
            foreach (var item in searchCandidates)
            {
                var condition = item.IsAndCondition ? Occur.MUST : Occur.MUST_NOT;

                switch (item.SearchType)
                {
                    case SearchType.Content:
                        query.Add(new TermQuery(new Term(nameof(CodeSource.Content), item.SearchText)), Occur.MUST);
                        break;

                    case SearchType.FileExtension:
                        query.Add(new TermQuery(new Term(nameof(CodeSource.FileExtension), item.SearchText)), Occur.MUST);
                        break;

                    case SearchType.FileName:
                        query.Add(new TermQuery(new Term(nameof(CodeSource.FileName), item.SearchText)), Occur.MUST);
                        break;

                    case SearchType.FilePath:
                        query.Add(new TermQuery(new Term(nameof(CodeSource.FilePath), item.SearchText)), Occur.MUST);
                        break;

                    default:
                        throw new ArgumentException(nameof(item.SearchType));
                }
            }

            return query;
        }

        public Query GetQueryFromStr(string searchStr)
        {
            searchStr.RequireNotNullOrEmpty(nameof(searchStr));

            return Parser.Parse(searchStr);
        }

        QueryParser Parser { get; set; } = new QueryParser(CodeIndexBuilder.AppLuceneVersion, nameof(CodeSource.Content), new StandardAnalyzer(CodeIndexBuilder.AppLuceneVersion));
    }
}