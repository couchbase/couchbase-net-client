{
  "status": {
    "total": 32,
    "failed": 0,
    "successful": 32
  },
  "request": {
    "query": {
      "query": "Adult",
      "boost": 1
    },
    "size": 10,
    "from": 0,
    "highlight": {
      "style": null,
      "fields": null
    },
    "fields": [ "*" ],
    "facets": null,
    "explain": true
  },
  "hits": [
    {
      "index": "travel_landmark_idx_699e0a42ee02c6b2_27184a97",
      "id": "landmark_22563",
      "score": 0.907210290772297,
      "explanation": {
        "value": 0.907210290772297,
        "message": "sum of:",
        "children": [
          {
            "value": 0.907210290772297,
            "message": "product of:",
            "children": [
              {
                "value": 0.907210290772297,
                "message": "sum of:",
                "children": [
                  {
                    "value": 0.907210290772297,
                    "message": "product of:",
                    "children": [
                      {
                        "value": 0.907210290772297,
                        "message": "sum of:",
                        "children": [
                          {
                            "value": 0.907210290772297,
                            "message": "fieldWeight(_all:adult in landmark_22563), product of:",
                            "children": [
                              {
                                "value": 1.4142135623730951,
                                "message": "tf(termFreq(_all:adult)=2"
                              },
                              {
                                "value": 0.09449111670255661,
                                "message": "fieldNorm(field=_all, doc=landmark_22563)"
                              },
                              {
                                "value": 6.788940282996508,
                                "message": "idf(docFreq=2, maxDocs=980)"
                              }
                            ]
                          }
                        ]
                      },
                      {
                        "value": 1,
                        "message": "coord(1/1)"
                      }
                    ]
                  }
                ]
              },
              {
                "value": 1,
                "message": "coord(1/1)"
              }
            ]
          }
        ]
      },
      "locations": {
        "price": {
          "adult": [
            {
              "pos": 1,
              "start": 0,
              "end": 5,
              "array_positions": null
            },
            {
              "pos": 4,
              "start": 18,
              "end": 23,
              "array_positions": null
            }
          ]
        }
      },
      "fields": { "content": "1.5 miles of the old Plymouth-Tavistock Great Western line, restored by local enthusiasts. Runs a number of old steam engines and other stock, which take visitors up this historic stretch of railway into Plym Woods." }
    },
    {
      "index": "travel_landmark_idx_699e0a42ee02c6b2_0a44bddb",
      "id": "landmark_25626",
      "score": 0.8537320114581743,
      "explanation": {
        "value": 0.8537320114581743,
        "message": "sum of:",
        "children": [
          {
            "value": 0.8537320114581743,
            "message": "product of:",
            "children": [
              {
                "value": 0.8537320114581743,
                "message": "sum of:",
                "children": [
                  {
                    "value": 0.8537320114581743,
                    "message": "product of:",
                    "children": [
                      {
                        "value": 0.8537320114581743,
                        "message": "sum of:",
                        "children": [
                          {
                            "value": 0.8537320114581743,
                            "message": "fieldWeight(_all:adult in landmark_25626), product of:",
                            "children": [
                              {
                                "value": 1.4142135623730951,
                                "message": "tf(termFreq(_all:adult)=2"
                              },
                              {
                                "value": 0.08873564749956131,
                                "message": "fieldNorm(field=_all, doc=landmark_25626)"
                              },
                              {
                                "value": 6.803124917988464,
                                "message": "idf(docFreq=2, maxDocs=994)"
                              }
                            ]
                          }
                        ]
                      },
                      {
                        "value": 1,
                        "message": "coord(1/1)"
                      }
                    ]
                  }
                ]
              },
              {
                "value": 1,
                "message": "coord(1/1)"
              }
            ]
          }
        ]
      },
      "locations": {
        "price": {
          "adult": [
            {
              "pos": 3,
              "start": 12,
              "end": 17,
              "array_positions": null
            },
            {
              "pos": 16,
              "start": 85,
              "end": 90,
              "array_positions": null
            }
          ]
        }
      },
      "fields": { "content": "Offers a 90 minute bay cruise on a 55-foot luxury Catamaran and also sunset cruises in the evening." }
    },
    {
      "index": "travel_landmark_idx_699e0a42ee02c6b2_9e13fa43",
      "id": "landmark_4120",
      "score": 0.7937631466938643,
      "explanation": {
        "value": 0.7937631466938643,
        "message": "sum of:",
        "children": [
          {
            "value": 0.7937631466938643,
            "message": "product of:",
            "children": [
              {
                "value": 0.7937631466938643,
                "message": "sum of:",
                "children": [
                  {
                    "value": 0.7937631466938643,
                    "message": "product of:",
                    "children": [
                      {
                        "value": 0.7937631466938643,
                        "message": "sum of:",
                        "children": [
                          {
                            "value": 0.7937631466938643,
                            "message": "fieldWeight(_all:adult in landmark_4120), product of:",
                            "children": [
                              {
                                "value": 1.4142135623730951,
                                "message": "tf(termFreq(_all:adult)=2"
                              },
                              {
                                "value": 0.09128709137439728,
                                "message": "fieldNorm(field=_all, doc=landmark_4120)"
                              },
                              {
                                "value": 6.148462999891583,
                                "message": "idf(docFreq=5, maxDocs=1033)"
                              }
                            ]
                          }
                        ]
                      },
                      {
                        "value": 1,
                        "message": "coord(1/1)"
                      }
                    ]
                  }
                ]
              },
              {
                "value": 1,
                "message": "coord(1/1)"
              }
            ]
          }
        ]
      },
      "locations": {
        "price": {
          "adult": [
            {
              "pos": 1,
              "start": 0,
              "end": 5,
              "array_positions": null
            },
            {
              "pos": 13,
              "start": 68,
              "end": 73,
              "array_positions": null
            }
          ]
        }
      },
      "fields": { "content": "From the hidden world of UK waters, this amazing new aquarium transports visitors to the spectacular 'underwater gardens' of the Mediterranean and stunning beauty of tropical waters - home to everything from seahorses and puffer fish to living corals and tropical sharks." }
    },
    {
      "index": "travel_landmark_idx_699e0a42ee02c6b2_d8c75a95",
      "id": "landmark_16532",
      "score": 0.7768936156987031,
      "explanation": {
        "value": 0.7768936156987031,
        "message": "sum of:",
        "children": [
          {
            "value": 0.7768936156987031,
            "message": "product of:",
            "children": [
              {
                "value": 0.7768936156987031,
                "message": "sum of:",
                "children": [
                  {
                    "value": 0.7768936156987031,
                    "message": "product of:",
                    "children": [
                      {
                        "value": 0.7768936156987031,
                        "message": "sum of:",
                        "children": [
                          {
                            "value": 0.7768936156987031,
                            "message": "fieldWeight(_all:adult in landmark_16532), product of:",
                            "children": [
                              {
                                "value": 1.4142135623730951,
                                "message": "tf(termFreq(_all:adult)=2"
                              },
                              {
                                "value": 0.08084520697593689,
                                "message": "fieldNorm(field=_all, doc=landmark_16532)"
                              },
                              {
                                "value": 6.795044065934526,
                                "message": "idf(docFreq=2, maxDocs=986)"
                              }
                            ]
                          }
                        ]
                      },
                      {
                        "value": 1,
                        "message": "coord(1/1)"
                      }
                    ]
                  }
                ]
              },
              {
                "value": 1,
                "message": "coord(1/1)"
              }
            ]
          }
        ]
      },
      "locations": {
        "price": {
          "adult": [
            {
              "pos": 3,
              "start": 19,
              "end": 24,
              "array_positions": null
            },
            {
              "pos": 24,
              "start": 149,
              "end": 154,
              "array_positions": null
            }
          ]
        }
      },
      "fields": { "content": "One of the largest aquariums in the United States, its nearly 1,000 species fill 19 major habitats and 32 focus exhibits and take visitors through three regions of the Pacific Ocean: Southern California/Baja, the Tropical Pacific, and the Northern Pacific.   There is also a Combo package with the Queen Mary. (Pay parking or take Passport C)" }
    },
    {
      "index": "travel_landmark_idx_699e0a42ee02c6b2_9e13fa43",
      "id": "landmark_10019",
      "score": 0.7539729082891258,
      "explanation": {
        "value": 0.7539729082891258,
        "message": "sum of:",
        "children": [
          {
            "value": 0.7539729082891258,
            "message": "product of:",
            "children": [
              {
                "value": 0.7539729082891258,
                "message": "sum of:",
                "children": [
                  {
                    "value": 0.7539729082891258,
                    "message": "product of:",
                    "children": [
                      {
                        "value": 0.7539729082891258,
                        "message": "sum of:",
                        "children": [
                          {
                            "value": 0.7539729082891258,
                            "message": "fieldWeight(_all:adult in landmark_10019), product of:",
                            "children": [
                              {
                                "value": 1.4142135623730951,
                                "message": "tf(termFreq(_all:adult)=2"
                              },
                              {
                                "value": 0.08671099692583084,
                                "message": "fieldNorm(field=_all, doc=landmark_10019)"
                              },
                              {
                                "value": 6.148462999891583,
                                "message": "idf(docFreq=5, maxDocs=1033)"
                              }
                            ]
                          }
                        ]
                      },
                      {
                        "value": 1,
                        "message": "coord(1/1)"
                      }
                    ]
                  }
                ]
              },
              {
                "value": 1,
                "message": "coord(1/1)"
              }
            ]
          }
        ]
      },
      "locations": {
        "content": {
          "adult": [
            {
              "pos": 1,
              "start": 0,
              "end": 5,
              "array_positions": null
            },
            {
              "pos": 5,
              "start": 22,
              "end": 27,
              "array_positions": null
            }
          ]
        }
      },
      "fragments": { "content": [ "\u003cmark\u003eAdult\u003c/mark\u003e - £6.99 for an \u003cmark\u003eAdult\u003c/mark\u003e ticket that allows you to come back for further visits within a year (children's and concessionary tickets also available). Museum on military engineering and the history of…" ] },
      "fields": { "content": "Adult - £6.99 for an Adult ticket that allows you to come back for further visits within a year (children's and concessionary tickets also available). Museum on military engineering and the history of the British Empire. A quite extensive collection that takes about half a day to see. Of most interest to fans of British and military history or civil engineering. The outside collection of tank mounted bridges etc can be seen for free. There is also an extensive series of themed special event weekends, admission to which is included in the cost of the annual ticket." }
    },
    {
      "index": "travel_landmark_idx_699e0a42ee02c6b2_f17535e5",
      "id": "landmark_21048",
      "score": 0.7430672327789425,
      "explanation": {
        "value": 0.7430672327789425,
        "message": "sum of:",
        "children": [
          {
            "value": 0.7430672327789425,
            "message": "product of:",
            "children": [
              {
                "value": 0.7430672327789425,
                "message": "sum of:",
                "children": [
                  {
                    "value": 0.7430672327789425,
                    "message": "product of:",
                    "children": [
                      {
                        "value": 0.7430672327789425,
                        "message": "sum of:",
                        "children": [
                          {
                            "value": 0.7430672327789425,
                            "message": "fieldWeight(_all:adult in landmark_21048), product of:",
                            "children": [
                              {
                                "value": 1.4142135623730951,
                                "message": "tf(termFreq(_all:adult)=2"
                              },
                              {
                                "value": 0.08362419903278351,
                                "message": "fieldNorm(field=_all, doc=landmark_21048)"
                              },
                              {
                                "value": 6.2832037287379885,
                                "message": "idf(docFreq=4, maxDocs=985)"
                              }
                            ]
                          }
                        ]
                      },
                      {
                        "value": 1,
                        "message": "coord(1/1)"
                      }
                    ]
                  }
                ]
              },
              {
                "value": 1,
                "message": "coord(1/1)"
              }
            ]
          }
        ]
      },
      "locations": {
        "content": {
          "adult": [
            {
              "pos": 23,
              "start": 118,
              "end": 123,
              "array_positions": null
            }
          ]
        },
        "price": {
          "adult": [
            {
              "pos": 10,
              "start": 33,
              "end": 38,
              "array_positions": null
            }
          ]
        }
      },
      "fragments": { "content": [ "One of the best preserved of 3 castles near the Dyke south-east of Llanvetherine. Under 16s must be accompanied by an \u003cmark\u003eadult\u003c/mark\u003e." ] },
      "fields": { "content": "One of the best preserved of 3 castles near the Dyke south-east of Llanvetherine. Under 16s must be accompanied by an adult." }
    },
    {
      "index": "travel_landmark_idx_699e0a42ee02c6b2_dee61dfa",
      "id": "landmark_28304",
      "score": 0.711030012265378,
      "explanation": {
        "value": 0.711030012265378,
        "message": "sum of:",
        "children": [
          {
            "value": 0.711030012265378,
            "message": "product of:",
            "children": [
              {
                "value": 0.711030012265378,
                "message": "sum of:",
                "children": [
                  {
                    "value": 0.711030012265378,
                    "message": "product of:",
                    "children": [
                      {
                        "value": 0.711030012265378,
                        "message": "sum of:",
                        "children": [
                          {
                            "value": 0.711030012265378,
                            "message": "weight(_all:adult^1.000000 in landmark_28304), product of:",
                            "children": [
                              {
                                "value": 0.9999999999999999,
                                "message": "queryWeight(_all:adult^1.000000), product of:",
                                "children": [
                                  {
                                    "value": 1,
                                    "message": "boost"
                                  },
                                  {
                                    "value": 7.181051314893349,
                                    "message": "idf(docFreq=1, maxDocs=967)"
                                  },
                                  {
                                    "value": 0.13925537587038558,
                                    "message": "queryNorm"
                                  }
                                ]
                              },
                              {
                                "value": 0.7110300122653781,
                                "message": "fieldWeight(_all:adult in landmark_28304), product of:",
                                "children": [
                                  {
                                    "value": 1,
                                    "message": "tf(termFreq(_all:adult)=1"
                                  },
                                  {
                                    "value": 0.0990147516131401,
                                    "message": "fieldNorm(field=_all, doc=landmark_28304)"
                                  },
                                  {
                                    "value": 7.181051314893349,
                                    "message": "idf(docFreq=1, maxDocs=967)"
                                  }
                                ]
                              }
                            ]
                          }
                        ]
                      },
                      {
                        "value": 1,
                        "message": "coord(1/1)"
                      }
                    ]
                  }
                ]
              },
              {
                "value": 1,
                "message": "coord(1/1)"
              }
            ]
          }
        ]
      },
      "locations": {
        "price": {
          "adult": [
            {
              "pos": 4,
              "start": 18,
              "end": 23,
              "array_positions": null
            }
          ]
        }
      },
      "fields": { "content": "- This building used to be the jail during Victorian times. A guide will show you through the building taking on different roles." }
    },
    {
      "index": "travel_landmark_idx_699e0a42ee02c6b2_348f5c3c",
      "id": "landmark_37140",
      "score": 0.687746353611814,
      "explanation": {
        "value": 0.687746353611814,
        "message": "sum of:",
        "children": [
          {
            "value": 0.687746353611814,
            "message": "product of:",
            "children": [
              {
                "value": 0.687746353611814,
                "message": "sum of:",
                "children": [
                  {
                    "value": 0.687746353611814,
                    "message": "product of:",
                    "children": [
                      {
                        "value": 0.687746353611814,
                        "message": "sum of:",
                        "children": [
                          {
                            "value": 0.687746353611814,
                            "message": "weight(_all:adult^1.000000 in landmark_37140), product of:",
                            "children": [
                              {
                                "value": 0.9999999999999999,
                                "message": "queryWeight(_all:adult^1.000000), product of:",
                                "children": [
                                  {
                                    "value": 1,
                                    "message": "boost"
                                  },
                                  {
                                    "value": 6.773515812670876,
                                    "message": "idf(docFreq=2, maxDocs=965)"
                                  },
                                  {
                                    "value": 0.14763381789547905,
                                    "message": "queryNorm"
                                  }
                                ]
                              },
                              {
                                "value": 0.6877463536118141,
                                "message": "fieldWeight(_all:adult in landmark_37140), product of:",
                                "children": [
                                  {
                                    "value": 1,
                                    "message": "tf(termFreq(_all:adult)=1"
                                  },
                                  {
                                    "value": 0.10153461992740631,
                                    "message": "fieldNorm(field=_all, doc=landmark_37140)"
                                  },
                                  {
                                    "value": 6.773515812670876,
                                    "message": "idf(docFreq=2, maxDocs=965)"
                                  }
                                ]
                              }
                            ]
                          }
                        ]
                      },
                      {
                        "value": 1,
                        "message": "coord(1/1)"
                      }
                    ]
                  }
                ]
              },
              {
                "value": 1,
                "message": "coord(1/1)"
              }
            ]
          }
        ]
      },
      "locations": {
        "price": {
          "adult": [
            {
              "pos": 1,
              "start": 0,
              "end": 5,
              "array_positions": null
            }
          ]
        }
      },
      "fields": { "content": "A tour of the constantly changing street art around this part of the East End. Saturday times may vary." }
    },
    {
      "index": "travel_landmark_idx_699e0a42ee02c6b2_76ac2a42",
      "id": "landmark_5200",
      "score": 0.6842354323891161,
      "explanation": {
        "value": 0.6842354323891161,
        "message": "sum of:",
        "children": [
          {
            "value": 0.6842354323891161,
            "message": "product of:",
            "children": [
              {
                "value": 0.6842354323891161,
                "message": "sum of:",
                "children": [
                  {
                    "value": 0.6842354323891161,
                    "message": "product of:",
                    "children": [
                      {
                        "value": 0.6842354323891161,
                        "message": "sum of:",
                        "children": [
                          {
                            "value": 0.6842354323891161,
                            "message": "fieldWeight(_all:adult in landmark_5200), product of:",
                            "children": [
                              {
                                "value": 1,
                                "message": "tf(termFreq(_all:adult)=1"
                              },
                              {
                                "value": 0.11180339753627777,
                                "message": "fieldNorm(field=_all, doc=landmark_5200)"
                              },
                              {
                                "value": 6.11998783102362,
                                "message": "idf(docFreq=5, maxDocs=1004)"
                              }
                            ]
                          }
                        ]
                      },
                      {
                        "value": 1,
                        "message": "coord(1/1)"
                      }
                    ]
                  }
                ]
              },
              {
                "value": 1,
                "message": "coord(1/1)"
              }
            ]
          }
        ]
      },
      "locations": {
        "price": {
          "adult": [
            {
              "pos": 2,
              "start": 8,
              "end": 13,
              "array_positions": null
            }
          ]
        }
      },
      "fields": { "content": "Features an underground boat ride." }
    },
    {
      "index": "travel_landmark_idx_699e0a42ee02c6b2_5c5f941e",
      "id": "landmark_5201",
      "score": 0.6770429444365057,
      "explanation": {
        "value": 0.6770429444365057,
        "message": "sum of:",
        "children": [
          {
            "value": 0.6770429444365057,
            "message": "product of:",
            "children": [
              {
                "value": 0.6770429444365057,
                "message": "sum of:",
                "children": [
                  {
                    "value": 0.6770429444365057,
                    "message": "product of:",
                    "children": [
                      {
                        "value": 0.6770429444365057,
                        "message": "sum of:",
                        "children": [
                          {
                            "value": 0.6770429444365057,
                            "message": "weight(_all:adult^1.000000 in landmark_5201), product of:",
                            "children": [
                              {
                                "value": 0.9999999999999999,
                                "message": "queryWeight(_all:adult^1.000000), product of:",
                                "children": [
                                  {
                                    "value": 1,
                                    "message": "boost"
                                  },
                                  {
                                    "value": 6.130884422247832,
                                    "message": "idf(docFreq=5, maxDocs=1015)"
                                  },
                                  {
                                    "value": 0.1631086040981603,
                                    "message": "queryNorm"
                                  }
                                ]
                              },
                              {
                                "value": 0.6770429444365058,
                                "message": "fieldWeight(_all:adult in landmark_5201), product of:",
                                "children": [
                                  {
                                    "value": 1,
                                    "message": "tf(termFreq(_all:adult)=1"
                                  },
                                  {
                                    "value": 0.11043152958154678,
                                    "message": "fieldNorm(field=_all, doc=landmark_5201)"
                                  },
                                  {
                                    "value": 6.130884422247832,
                                    "message": "idf(docFreq=5, maxDocs=1015)"
                                  }
                                ]
                              }
                            ]
                          }
                        ]
                      },
                      {
                        "value": 1,
                        "message": "coord(1/1)"
                      }
                    ]
                  }
                ]
              },
              {
                "value": 1,
                "message": "coord(1/1)"
              }
            ]
          }
        ]
      },
      "locations": {
        "price": {
          "adult": [
            {
              "pos": 2,
              "start": 8,
              "end": 13,
              "array_positions": null
            }
          ]
        }
      },
      "fields": { "content": "Blue John seams, craft shop showing Blue John ornaments." }
    }
  ],
  "total_hits": 116,
  "max_score": 0.907210290772297,
  "took": 123165714,
  "facets": {}
}
