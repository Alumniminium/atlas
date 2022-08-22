openssl req -new -newkey rsa:4096 -days 365 -nodes -x509 \
    -subj "/C=US/ST=Denial/L=Springfield/O=Dis/CN=$1" \
    -keyout $1.key  -out $1.cert

openssl pkcs12 -export -out $1.pfx -inkey $1.key -in $1.cert