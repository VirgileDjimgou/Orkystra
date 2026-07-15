# Gradle Wrapper

Le binaire Wrapper n'est pas inclus dans ce kit généré. Pendant SPRINT-00, ouvrir ce dossier avec Android Studio ou exécuter avec un Gradle local stable :

```bash
gradle wrapper --gradle-version 9.6.1
./gradlew tasks
```

Committer ensuite `gradlew`, `gradlew.bat`, `gradle/wrapper/gradle-wrapper.jar` et `gradle-wrapper.properties`. Vérifier la compatibilité exacte AGP/Gradle dans la documentation officielle avant de verrouiller la version.
