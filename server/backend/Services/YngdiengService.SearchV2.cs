﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Microsoft.Extensions.Logging;
using Yngdieng.Common;
using Yngdieng.Protos;
using Yngdieng.Search.Common;
using LuceneQuery = Lucene.Net.Search.Query;

namespace Yngdieng.Backend.Services
{
    public partial class YngdiengService : Yngdieng.Protos.YngdiengService.YngdiengServiceBase
    {

        private static readonly int DEFAULT_PAGE_SIZE = 10;

        public override Task<SearchV2Response> SearchV2(SearchV2Request request,
                                                        ServerCallContext context)
        {
            var query = GetLuceneQuery(QueryParser.Parse(request.Query));
            var desiredPageSize = request.PageSize > 0 ? request.PageSize : DEFAULT_PAGE_SIZE;
            var searcher = this._indexHolder.LuceneIndexSearcher;

            var resultCards = new List<SearchV2Response.Types.SearchCard>();

            TopDocs results;
            if (string.IsNullOrEmpty(request.PageToken))
            {
                // Is first page 
                resultCards.Add(GenericMessageCard("你正在試用榕典搜索V2。如遇問題，請將截圖和網址發送到 radium@mindong.asia。"));
                results = searcher.Search(query, desiredPageSize + 1);
            }
            else
            {
                var lastPage = PaginationTokens.Parse(request.PageToken);
                var lastScoreDoc = new ScoreDoc(lastPage.LastDoc.Doc, lastPage.LastDoc.Score);
                results = searcher.SearchAfter(lastScoreDoc, query, desiredPageSize + 1);
            }

            var response = new SearchV2Response();
            var isEndOfResults = results.ScoreDocs.Length < desiredPageSize + 1;
            if (isEndOfResults)
            {
                resultCards.AddRange(RenderDocs(results.ScoreDocs));
                resultCards.Add(EndOfResultsCard());
            }
            else
            {
                var visibleRange = results.ScoreDocs.Take(desiredPageSize);
                var nextPageToken = PaginationTokens.FromScoreDoc(visibleRange.Last());
                response.NextPageToken = nextPageToken;
                resultCards.AddRange(RenderDocs(visibleRange));

                // debug only
                resultCards.Add(GenericMessageCard($"next_page_token: {nextPageToken}"));
            }

            response.ResultCards.AddRange(resultCards);
            return Task.FromResult(response);
        }

        private LuceneQuery GetLuceneQuery(Yngdieng.Protos.Query query)
        {
            switch (query.QueryCase)
            {
                case Yngdieng.Protos.Query.QueryOneofCase.HanziQuery:
                    return HandleHanziQuery(query.HanziQuery);
                case Yngdieng.Protos.Query.QueryOneofCase.YngpingTonePatternQuery:
                    return HandleYngpingTonePatternQuery(query.YngpingTonePatternQuery);
                case Yngdieng.Protos.Query.QueryOneofCase.FuzzyPronQuery:
                    return HandleFuzzyYngpingQuery(query.FuzzyPronQuery);
                default:
                    throw new ArgumentException($"Unsupported query type: {query.QueryCase}");
            }
        }

        private static LuceneQuery HandleFuzzyYngpingQuery(string queryText)
        {
            var queryParser = new MultiFieldQueryParser(LuceneUtils.AppLuceneVersion,
            new string[] { LuceneUtils.Fields.Yngping },
                        LuceneUtils.GetAnalyzer(),
                        new Dictionary<string, float>{
                {LuceneUtils.Fields.Yngping, 100},
                        });
            return queryParser.Parse(queryText);
        }

        private static LuceneQuery HandleYngpingTonePatternQuery(string queryText)
        {
            return new WildcardQuery(new Lucene.Net.Index.Term(LuceneUtils.Fields.YngpingSandhiTonePattern, queryText));
        }

        private static LuceneQuery HandleHanziQuery(string queryText)
        {
            var queryParser = new MultiFieldQueryParser(LuceneUtils.AppLuceneVersion,
            new string[] { LuceneUtils.Fields.Hanzi, LuceneUtils.Fields.Explanation },
                        LuceneUtils.GetAnalyzer(),
                        new Dictionary<string, float>{
                {LuceneUtils.Fields.Hanzi, 100},
                {LuceneUtils.Fields.Explanation, 1}
                        });
            return queryParser.Parse(queryText);
        }

        private static string FindBestExplanation(YngdiengDocument ydDoc)
        {
            var fengExplation = ydDoc.Sources
                .FirstOrDefault(s => s.SourceCase == YngdiengDocument.Types.Source.SourceOneofCase.Feng)
                ?.Feng.ExplanationTrad;
            if (fengExplation != null)
            {
                return fengExplation.Truncate(100);
            }
            var contribExplanation = ydDoc.Sources
                        .FirstOrDefault(s => s.SourceCase == YngdiengDocument.Types.Source.SourceOneofCase.Contrib)
                        ?.Contrib.ExplanationRaw; //TODO:fix. explanation flattened.
            if (contribExplanation != null)
            {
                return contribExplanation.Truncate(100);
            }
            return string.Empty;
        }

        private IEnumerable<SearchV2Response.Types.SearchCard> RenderDocs(IEnumerable<ScoreDoc> scoreDocs)
        {
            var searcher = this._indexHolder.LuceneIndexSearcher;

            return scoreDocs.Select(sd =>
           {
               var docId = searcher.Doc(sd.Doc).GetField(LuceneUtils.Fields.DocId).GetStringValue();
               var ydDoc = _indexHolder.GetIndex().YngdiengDocuments.Single(y => y.DocId == docId);

               return new SearchV2Response.Types.SearchCard
               {
                   Word = new SearchV2Response.Types.SearchCard.Types.WordCard
                   {
                       Id = docId,
                       Yngping = RichTextUtil.FromString(ydDoc.YngpingSandhi.OrElse(ydDoc.YngpingUnderlying)),
                       Hanzi = RichTextUtil.FromString(HanziUtils.HanziToString(ydDoc.HanziCanonical)),
                       Details = RichTextUtil.FromString(FindBestExplanation(ydDoc)),
                       Score = sd.Score
                   }
               };
           });
        }

        private static SearchV2Response.Types.SearchCard GenericMessageCard(string message)
        {
            return new SearchV2Response.Types.SearchCard
            {
                GenericMessage = new SearchV2Response.Types.SearchCard.Types
                                                          .GenericMessageCard()
                {
                    Message = RichTextUtil.FromString(message)
                }
            };
        }
        private static SearchV2Response.Types.SearchCard EndOfResultsCard()
        {
            return new SearchV2Response.Types.SearchCard
            {
                EndOfResults = new SearchV2Response.Types.SearchCard.Types
                                                          .EndOfResultsCard()
            };
        }
    }

}