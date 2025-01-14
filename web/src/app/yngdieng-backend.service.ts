import {Inject, Injectable} from '@angular/core';
import {Observable, Subject} from 'rxjs';
import {FengDocument, HistoricalDocument} from 'yngdieng/shared/documents_pb';
import {
  DebugInfo,
  GetAggregatedDocumentRequest,
  GetDebugInfoRequest,
  GetFengDocumentRequest,
  SearchRequest,
  SearchResponse,
  SearchV2Request,
  SearchV2Response,
  SimplifyTextRequest,
  GenerateSandhiRequest,
  GenerateSandhiResponse,
  UserPreference,
} from 'yngdieng/shared/services_pb';
import {YngdiengServiceClient} from 'yngdieng/shared/services_grpc_web_pb';
import {
  Word,
  GetWordRequest,
  ListWordListWordsRequest,
  ListWordListWordsResponse,
} from 'yngdieng/yngdieng/frontend/v3/service_pb';
import {FrontendServiceClient} from 'yngdieng/yngdieng/frontend/v3/service_grpc_web_pb';

import {
  IYngdiengEnvironment,
  YNGDIENG_ENVIRONMENT,
} from '../environments/environment';
import * as jspb from 'google-protobuf';
import {UserSettingsService} from './user-settings.service';
@Injectable({providedIn: 'root'})
export class YngdiengBackendService {
  // Deprecated
  private grpcClient: YngdiengServiceClient;
  private frontendGrpcClient: FrontendServiceClient;

  constructor(
    @Inject(YNGDIENG_ENVIRONMENT) private environment: IYngdiengEnvironment,
    private userSettings: UserSettingsService
  ) {
    this.grpcClient = new YngdiengServiceClient(this.environment.serverUrl);
    this.frontendGrpcClient = new FrontendServiceClient(
      this.environment.serverUrl
    );
  }

  search(queryText: string, offset: number = 0): Observable<SearchResponse> {
    let subject = new Subject<SearchResponse>();
    let request = new SearchRequest();
    request.setQuery(queryText);
    request.setOffset(offset);

    this.grpcClient.search(request, undefined, (err, response) => {
      if (err != null) {
        subject.error(err);
        return;
      }
      subject.next(response);
      subject.complete();
    });

    return subject.asObservable();
  }

  searchV2(queryText: string, pageToken: string): Observable<SearchV2Response> {
    let subject = new Subject<SearchV2Response>();
    let request = new SearchV2Request();
    request.setQuery(queryText);
    request.setPageToken(pageToken);
    // TODO: parameterize
    request.setPageSize(15);

    this.grpcClient.searchV2(
      request,
      {'x-ydict-options': this.getUserPreference()},
      (err, response) => {
        if (err != null) {
          subject.error(err);
          return;
        }
        subject.next(response);
        subject.complete();
      }
    );

    return subject.asObservable();
  }

  listWordListWords(
    wordListName: string,
    pageToken: string
  ): Observable<ListWordListWordsResponse> {
    let subject = new Subject<ListWordListWordsResponse>();
    let request = new ListWordListWordsRequest();
    request.setParent(wordListName);
    request.setPageToken(pageToken);
    request.setPageSize(15);

    this.frontendGrpcClient.listWordListWords(
      request,
      {'x-ydict-options': this.getUserPreference()},
      (err, response) => {
        if (err != null) {
          subject.error(err);
          return;
        }
        subject.next(response);
        subject.complete();
      }
    );

    return subject.asObservable();
  }

  private getUserPreference() {
    let userPreference = new UserPreference();
    userPreference.setZhConversionPreference(
      this.userSettings.getZhConversionPreference()
    );
    userPreference.setShowSourcelessSearchResults(
      this.userSettings.getShowSourcelessSearchResults()
    );
    return jspb.Message.bytesAsB64(userPreference.serializeBinary());
  }

  getFengDocument(fengDocId: string): Observable<FengDocument> {
    let subject = new Subject<FengDocument>();
    let request = new GetFengDocumentRequest();
    request.setId(fengDocId);
    this.grpcClient.getFengDocument(request, undefined, (err, response) => {
      if (err != null) {
        subject.error(err);
        return;
      }
      subject.next(response);
      subject.complete();
    });
    return subject.asObservable();
  }

  getWord(docId: string): Observable<Word> {
    let subject = new Subject<Word>();
    let request = new GetWordRequest();
    request.setName(`words/${docId}`);
    this.frontendGrpcClient.getWord(
      request,
      {'x-ydict-options': this.getUserPreference()},
      (err, response) => {
        if (err != null) {
          subject.error(err);
          return;
        }
        subject.next(response);
        subject.complete();
      }
    );
    return subject.asObservable();
  }

  getHistoricalDocument(docId: string): Observable<HistoricalDocument> {
    let subject = new Subject<HistoricalDocument>();
    let request = new GetAggregatedDocumentRequest();
    request.setId(docId);
    this.grpcClient.getAggregatedDocument(
      request,
      undefined,
      (err, response) => {
        if (err != null) {
          subject.error(err);
          return;
        }

        subject.next(response);
        subject.complete();
      }
    );

    return subject.asObservable();
  }

  getDebugInfo(): Observable<DebugInfo> {
    let subject = new Subject<DebugInfo>();
    this.grpcClient.getDebugInfo(
      new GetDebugInfoRequest(),
      undefined,
      (err, response) => {
        if (err != null) {
          subject.error(err);
          return;
        }
        subject.next(response);
        subject.complete();
      }
    );

    return subject.asObservable();
  }

  simplifyText(text: string): Observable<string> {
    let subject = new Subject<string>();
    let request = new SimplifyTextRequest();
    request.setText(text);
    this.grpcClient.simplifyText(request, undefined, (err, response) => {
      if (err != null) {
        subject.error(err);
        return;
      }
      subject.next(response.getConvertedText());
      subject.complete();
    });

    return subject.asObservable();
  }

  generateSandhi(inputs: string[]): Observable<GenerateSandhiResponse> {
    let subject = new Subject<GenerateSandhiResponse>();
    let request = new GenerateSandhiRequest();
    request.setInputsList(inputs);
    this.grpcClient.generateSandhi(request, undefined, (err, response) => {
      if (err != null) {
        subject.error(err);
        return;
      }
      subject.next(response);
      subject.complete();
    });

    return subject.asObservable();
  }
}
