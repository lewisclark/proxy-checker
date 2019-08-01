using System;
using System.Windows.Forms;
using System.Drawing;
using System.Threading.Tasks;
using System.Net;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;

namespace ProxyChecker {
	public class ProxyChecker {
		private const int ChunkSize = 25;

		public static void Main() {
			Application.Run(new ProxyCheckerForm());
		}

		public static async Task CheckProxiesAsync(List<WebProxy> proxies, string website, int timeoutSecs, IProgress<ProxyCheckProgressReport> progress, CancellationToken cancellationToken) {
			int numTotal = proxies.Count;
			int numChecked = 0;
			int chunkSize = Math.Min(ChunkSize, proxies.Count);

			foreach (List<WebProxy> splitProxies in ListExtensions.ChunkBy(proxies, chunkSize)) {
				if (cancellationToken.IsCancellationRequested) {
					break;
				}

				List<Task<ProxyCheckResult>> tasks = new List<Task<ProxyCheckResult>>();

				foreach (WebProxy proxy in splitProxies) {
					tasks.Add(Task.Run(() => {
						Task<ProxyCheckResult> result = CheckProxyAsync(proxy, website, timeoutSecs);

						progress.Report(new ProxyCheckProgressReport() {
							NumTotal = numTotal,
							NumChecked = ++numChecked,
							ProxyChecked = proxy,
							ProxyCheckResult = result.Result
						});

						return result;
					}));
				}

				await Task.WhenAll(tasks);
			}
		}

		public static async Task<ProxyCheckResult> CheckProxyAsync(WebProxy proxy, string website, int timeoutSecs) {
			HttpClientHandler clienthandler = new HttpClientHandler() {
				Proxy = proxy,
				UseProxy = true
			};
			HttpClient client = new HttpClient(clienthandler) {
				Timeout = new TimeSpan(0, 0, timeoutSecs)
			};
			ProxyCheckResult result = ProxyCheckResult.UNKNOWN;

			try {
				HttpResponseMessage resp = await client.GetAsync(website);

				switch (resp.StatusCode) {
					case HttpStatusCode.OK:
						result = ProxyCheckResult.OK;
						break;
					default:
						result = ProxyCheckResult.UNKNOWN;
						break;
				}
			}
			catch (Exception ex) {
				if (ex is TaskCanceledException || ex is HttpRequestException) {
					result = ProxyCheckResult.TIMED_OUT;
				}
			}

			clienthandler.Dispose();
			client.Dispose();

			return result;
		}
	}
}