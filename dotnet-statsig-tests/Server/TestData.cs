using System;
using System.Collections.Generic;

namespace dotnet_statsig_tests
{
    public abstract class TestData
    {
        public static string layerInitialize = @"
            {
              'feature_gates': {},
              'dynamic_configs': {},
              'layer_configs': {
                'VUVogMV1FuIxgfJ9HUkvcckQofFSnOJ1ydZpl5KXC0U=': {
                  '____UNHASHED_NAME': 'unallocated_layer',
                  'name': 'VUVogMV1FuIxgfJ9HUkvcckQofFSnOJ1ydZpl5KXC0U=',
                  'value': {
                    'a_bool': true,
                    'an_int': 99,
                    'a_double': 12.34,
                    'a_long': 9223372036854775806,
                    'a_string': 'value',
                    'an_array': ['a','b'],
                    'an_object': {'c': 'd'},
                  },
                  'rule_id': 'default',
                  'group': '4wAsx5uAYntXGDy7jEwfb6',
                  'allocated_experiment_name': '',
                  'is_device_based': false,
                  'is_experiment_active': true,
                  'explicit_parameters': [],
                  'is_user_in_experiment': true,
                  'secondary_exposures': [{
                        'gate': 'secondary_exp',
                        'gateValue': 'false',
                        'ruleID': 'default'
                    }],
                  'undelegated_secondary_exposures': [{
                        'gate': 'undelegated_secondary_exp',
                        'gateValue': 'false',
                        'ruleID': 'default'
                    }]
                },
                'vQFNFFjIekL9Dw9KZx9yDlQxnh8pEV1XSO1Q2KyLqjY=': {
                  '____UNHASHED_NAME': 'allocated_layer',
                  'name': 'vQFNFFjIekL9Dw9KZx9yDlQxnh8pEV1XSO1Q2KyLqjY=',
                  'value': {
                    'explicit_key': 'from_exp',
                    'implicit_key': 'from_layer',
                  },
                  'rule_id': 'default',
                  'group': '4wAsx5uAYntXGDy7jEwfb6',
                  'allocated_experiment_name': 'an_experiment',
                  'is_device_based': false,
                  'is_experiment_active': true,
                  'explicit_parameters': ['explicit_key'],
                  'is_user_in_experiment': true,
                  'secondary_exposures': [{
                        'gate': 'secondary_exp',
                        'gateValue': 'false',
                        'ruleID': 'default'
                    }],
                  'undelegated_secondary_exposures': [{
                        'gate': 'undelegated_secondary_exp',
                        'gateValue': 'false',
                        'ruleID': 'default'
                    }]
                }
              },
              'sdkParams': {},
              'has_updates': true,
              'time': 1648749618359
            }
        ";

        public static string layerExposuresDownloadConfigSpecs = @"
			{
              'has_updates': true,
              'feature_gates': [],
              'dynamic_configs': [
                {
                  '__________________________USED_BY_TESTS': [
                    'test_explicit_vs_implicit_parameter_logging'
                  ],
                  'name': 'experiment',
                  'type': 'dynamic_config',
                  'salt': '58d0f242-4533-4601-abf7-126aa8f43868',
                  'enabled': true,
                  'defaultValue': {
                    'an_int': 0,
                    'a_string': 'layer_default'
                  },
                  'rules': [
                    {
                      'name': 'alwaysPass',
                      'groupName': 'Public',
                      'passPercentage': 100,
                      'conditions': [
                        {
                          'type': 'public',
                          'targetValue': null,
                          'operator': null,
                          'field': null,
                          'additionalValues': {},
                          'isDeviceBased': false,
                          'idType': 'userID'
                        }
                      ],
                      'returnValue': {
                        'an_int': 99,
                        'a_string': 'exp_value'
                      },
                      'id': 'alwaysPass',
                      'salt': '',
                      'isDeviceBased': false,
                      'idType': 'userID'
                    }
                  ],
                  'isDeviceBased': false,
                  'idType': 'userID',
                  'entity': 'experiment',
                  'explicitParameters': [
                    'an_int'
                  ]
                }
              ],
              'layer_configs': [
                {
                  '__________________________USED_BY_TESTS': [
                    'test_does_not_log_on_get_layer',
                    'test_does_not_log_on_invalid_type',
                    'test_does_not_log_non_existent_keys',
                    'test_unallocated_layer_logging',
                    'test_logs_user_and_event_name'
                  ],
                  'name': 'unallocated_layer',
                  'type': 'dynamic_config',
                  'salt': '3e361046-bc69-4dfd-bbb1-538afe609157',
                  'enabled': true,
                  'defaultValue': {
                    'an_int': 99
                  },
                  'rules': [],
                  'isDeviceBased': false,
                  'idType': 'userID',
                  'entity': 'layer'
                },
                {
                  '__________________________USED_BY_TESTS': [
                    'test_explicit_vs_implicit_parameter_logging'
                  ],
                  'name': 'explicit_vs_implicit_parameter_layer',
                  'type': 'dynamic_config',
                  'salt': '3e361046-bc69-4dfd-bbb1-538afe609157',
                  'enabled': true,
                  'defaultValue': {
                    'an_int': 0,
                    'a_string': 'layer_default'
                  },
                  'rules': [
                    {
                      'name': 'experimentAssignment',
                      'groupName': 'Experiment Assignment',
                      'passPercentage': 100,
                      'conditions': [
                        {
                          'type': 'public',
                          'targetValue': null,
                          'operator': null,
                          'field': null,
                          'additionalValues': {},
                          'isDeviceBased': false,
                          'idType': 'userID'
                        }
                      ],
                      'returnValue': {
                        'an_int': 0,
                        'a_string': 'layer_default'
                      },
                      'id': 'experimentAssignment',
                      'salt': '',
                      'isDeviceBased': false,
                      'idType': 'userID',
                      'configDelegate': 'experiment'
                    }
                  ],
                  'isDeviceBased': false,
                  'idType': 'userID',
                  'entity': 'layer'
                },
                {
                  '__________________________USED_BY_TESTS': [
                    'test_different_object_type_logging'
                  ],
                  'name': 'different_object_type_logging_layer',
                  'type': 'dynamic_config',
                  'salt': '3e361046-bc69-4dfd-bbb1-538afe609157',
                  'enabled': true,
                  'defaultValue': {
                    'a_bool': true,
                    'an_int': 99,
                    'a_float': 3.4028235E+38,
                    'a_double': 1.7976931348623157E+308,
                    'a_long': 9223372036854775807,
                    'a_ulong': 18446744073709551615,
                    'a_string': 'value',
                    'an_array': [
                      'a',
                      'b'
                    ],
                    'an_object': {
                      'key': 'value'
                    },
                    'another_object': {
                      'another_key': 'another_value'
                    }
                  },
                  'rules': [],
                  'isDeviceBased': false,
                  'idType': 'userID',
                  'entity': 'layer'
                }
              ]
            }
		";

        public static Dictionary<string, object> ClientInitializeResponse = new Dictionary<string, object>
        {
          {
            "feature_gates", new Dictionary<string, object>
            {
              {
                "AoZS0F06Ub+W2ONx+94rPTS7MRxuxa+GnXro5Q1uaGY=",
                new Dictionary<string, object>
                {
                  { "value", true },
                  { "rule_id", "ruleID" },
                  { "name", "AoZS0F06Ub+W2ONx+94rPTS7MRxuxa+GnXro5Q1uaGY=" },
                  {
                    "secondary_exposures",
                    new List<Dictionary<string, string>>
                    {
                      new Dictionary<string, string>
                      {
                        { "gate", "dependent_gate_1" },
                        { "gateValue", "true" },
                        { "ruleID", "rule_1" },
                      },
                      new Dictionary<string, string>
                      {
                        { "gate", "dependent_gate_2" },
                        { "gateValue", "false" },
                        { "ruleID", "rule_2" },
                      },
                    }
                  }
                }
              }
            }
          },
          {
            "dynamic_configs", new Dictionary<string, object>
            {
              {
                "RMv0YJlLOBe7cY7HgZ3Jox34R0Wrk7jLv3DZyBETA7I=",
                new Dictionary<string, object>
                {
                  {
                    "value",
                    new Dictionary<string, object>
                      { { "stringValue", "1" }, { "numberValue", 1 }, { "boolValue", true } }
                  },
                  { "rule_id", "ruleID" },
                  { "name", "RMv0YJlLOBe7cY7HgZ3Jox34R0Wrk7jLv3DZyBETA7I=" },
                  {
                    "secondary_exposures",
                    new List<Dictionary<string, string>>
                    {
                      new Dictionary<string, string>
                      {
                        { "gate", "dependent_gate_1" },
                        { "gateValue", "true" },
                        { "ruleID", "rule_1" },
                      },
                      new Dictionary<string, string>
                      {
                        { "gate", "dependent_gate_2" },
                        { "gateValue", "false" },
                        { "ruleID", "rule_2" },
                      },
                    }
                  }
                }
              }
            }
          }
        };
    }
}
