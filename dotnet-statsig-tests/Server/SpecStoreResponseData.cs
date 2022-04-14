using System.Collections.Generic;
using Statsig.Server;

namespace dotnet_statsig_tests
{
	public class SpecStoreResponseData
	{
		internal static string downloadConfigSpecResponse =
            @"
            {
               'has_updates': true,
               'feature_gates': [],
               'dynamic_configs': [],
               'layer_configs': []
            }
			";

        internal static string getIDList1Response(int index)
        {
            var responses = new string[]
            {
                "+1\n",
                "-1\n+2\n",
                "+3\n",
                "3",
                "+3\n+4\n+5\n+4\n-4\n+6\n+6\n+5\n",
            };
            return index >= responses.Length ? responses[responses.Length - 1] : responses[index];
        }

        internal static Dictionary<string, IDList[]> getIDListExpectedResults(string baseURL)
        {
            var expectedList1 = new IDList[]
            {
                idListWithIDs("list_1", 3, baseURL + "/list_1", 1, "file_id_1", new string[] { "1" }),
                idListWithIDs("list_1", 9, baseURL + "/list_1", 1, "file_id_1", new string[] { "2" }),
                idListWithIDs("list_1", 3, baseURL + "/list_1", 3, "file_id_1_a", new string[] { "3" }),
                idListWithIDs("list_1", 3, baseURL + "/list_1", 3, "file_id_1_a", new string[] { "3" }),
                null,
                idListWithIDs("list_1", 24, baseURL + "/list_1", 3, "file_id_1_a", new string[] { "3", "5", "6" }),
            };
            var expectedList2 = new IDList[]
            {
                idListWithIDs("list_2", 3, baseURL + "/list_2", 1, "file_id_2", new string[] { "a" }),
                null,
                null,
                null,
                null,
                null,
            };
            var expectedList3 = new IDList[]
            {
                null,
                null,
                null,
                null,
                idListWithIDs("list_3", 3, baseURL + "/list_3", 5, "file_id_3", new string[] { "0" }),
                idListWithIDs("list_3", 3, baseURL + "/list_3", 5, "file_id_3", new string[] { "0" }),
            };
            return new Dictionary<string, IDList[]>
            {
                {"list_1", expectedList1 },
                {"list_2", expectedList2 },
                {"list_3", expectedList3 },
            };
        }

        private static IDList idListWithIDs(string name, double size, string url, double creationTime, string fileID, string[] ids)
        {
            var list = new IDList
            {
                Name = name,
                Size = size,
                URL = url,
                CreationTime = creationTime,
                FileID = fileID,
            };
            foreach (var id in ids)
            {
                list.IDs.TryAdd(id, true);
            }
            return list;
        }

        internal static string getIDListsResponse(string baseURL, int index)
        {
            var url1 = baseURL + "/list_1";
            var url2 = baseURL + "/list_2";
            var url3 = baseURL + "/list_3";
            var responses = new string[]
            {
                // 0
                $@"{{
                    'list_1': {{
                        'name': 'list_1',
                        'size': 3,
                        'url': '{url1}',
                        'creationTime': 1,
                        'fileID': 'file_id_1',
                    }},
                    'list_2': {{
                        'name': 'list_2',
                        'size': 3,
                        'url': '{url2}',
                        'creationTime': 1,
                        'fileID': 'file_id_2',
                    }},
                }}",
                // 1
                $@"{{
                    'list_1': {{
                        'name': 'list_1',
                        'size': 9,
                        'url': '{url1}',
                        'creationTime': 1,
                        'fileID': 'file_id_1',
                    }},
                }}",
                // 2
                $@"{{
                    'list_1': {{
                        'name': 'list_1',
                        'size': 3,
                        'url': '{url1}',
                        'creationTime': 3,
                        'fileID': 'file_id_1_a',
                    }},
                }}",
                // 3
                $@"{{
                    'list_1': {{
                        'name': 'list_1',
                        'size': 9,
                        'url': '{url1}',
                        'creationTime': 1,
                        'fileID': 'file_id_1',
                    }},
                }}",
                // 4
                $@"{{
                    'list_1': {{
                        'name': 'list_1',
                        'size': 24,
                        'url': '{url1}',
                        'creationTime': 3,
                        'fileID': 'file_id_1_a',
                    }},
                    'list_3': {{
                        'name': 'list_3',
                        'size': 3,
                        'url': '{url3}',
                        'creationTime': 5,
                        'fileID': 'file_id_3',
                    }},
                }}",
            };
            return index >= responses.Length ? responses[responses.Length - 1] : responses[index];
        }
    }
}

