#!/usr/bin/env bash

docker -D build -t herstfortress/atlas:latest . && docker push herstfortress/atlas:latest